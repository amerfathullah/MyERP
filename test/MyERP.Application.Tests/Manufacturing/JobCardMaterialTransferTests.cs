using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Regression coverage for ERPNext PR #60246 (commit c18d8a4237):
/// keep job card qty in material request transfer.
/// Also exercises the full JobCard -> MaterialRequest -> StockEntry transfer lifecycle:
/// - Creating Material Request from Job Card carrying JobCardId and WorkOrderId
/// - Preserving FgCompletedQty = job_card.for_quantity when JobCardId present on Material Request
/// - Setting FgCompletedQty = 0 when only WorkOrderId present on Material Request
/// - Updating JobCard.TransferredQty and transfer status on Stock Entry post and cancel.
/// </summary>
public abstract class JobCardMaterialTransferTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateMaterialRequestAsync_CreatesLinkedMaterialRequest()
    {
        var (company, fgItem, rmItem, wo, jc, sourceWh, wipWh) = await SeedAsync("MR1", forQty: 10m, requiredQty: 20m);
        var jobCardAppService = GetRequiredService<IJobCardAppService>();
        var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();

        var mrDto = await jobCardAppService.CreateMaterialRequestAsync(jc.Id);

        mrDto.ShouldNotBeNull();
        mrDto.JobCardId.ShouldBe(jc.Id);
        mrDto.WorkOrderId.ShouldBe(wo.Id);
        mrDto.RequestType.ShouldBe(MaterialRequestType.MaterialTransfer);
        mrDto.TargetWarehouseId.ShouldBe(wipWh);
        mrDto.Items.Count.ShouldBe(1);
        mrDto.Items[0].ItemId.ShouldBe(rmItem.Id);
        mrDto.Items[0].Quantity.ShouldBe(20m);
        mrDto.Items[0].JobCardItemId.ShouldBe(wo.RequiredItems[0].Id);

        var persistedMr = await mrRepo.GetAsync(mrDto.Id);
        persistedMr.JobCardId.ShouldBe(jc.Id);
        persistedMr.WorkOrderId.ShouldBe(wo.Id);
        persistedMr.Items.Single().JobCardItemId.ShouldBe(wo.RequiredItems[0].Id);
    }

    [Fact]
    public async Task CreateMaterialRequestAsync_WhenAllMaterialsTransferred_Throws()
    {
        var (company, fgItem, rmItem, wo, jc, sourceWh, wipWh) = await SeedAsync("NO_PENDING", forQty: 10m, requiredQty: 20m);
        var woItemRepo = GetRequiredService<IRepository<WorkOrderItem, Guid>>();
        var jobCardAppService = GetRequiredService<IJobCardAppService>();

        // Mark all materials transferred on WorkOrder
        var woItem = await woItemRepo.GetAsync(wo.RequiredItems[0].Id);
        woItem.TransferredQuantity = 20m;
        await woItemRepo.UpdateAsync(woItem, autoSave: true);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            jobCardAppService.CreateMaterialRequestAsync(jc.Id));
        ex.Code.ShouldBe("MyERP:10013");
    }

    [Fact]
    public async Task GetItemsFromMaterialRequestAsync_WithJobCard_KeepsJobCardForQuantity()
    {
        // Regression test for ERPNext PR #60246 (commit c18d8a4237):
        // When Material Request has JobCardId, fg_completed_qty must be preserved from JobCard.ForQuantity
        var (company, fgItem, rmItem, wo, jc, sourceWh, wipWh) = await SeedAsync("MR_JC", forQty: 12m, requiredQty: 24m);
        var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();

        var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-JC-001", MaterialRequestType.MaterialTransfer, DateTime.Today)
        {
            JobCardId = jc.Id,
            WorkOrderId = wo.Id,
            TargetWarehouseId = wipWh,
        };
        mr.AddItem(rmItem.Id, rmItem.ItemName, 24m, "Unit", warehouseId: wipWh, jobCardItemId: wo.RequiredItems[0].Id);
        mr.Submit();
        await mrRepo.InsertAsync(mr, autoSave: true);

        var result = await stockEntryAppService.GetItemsFromMaterialRequestAsync(mr.Id);

        result.FgCompletedQty.ShouldBe(12m);
        result.JobCardId.ShouldBe(jc.Id);
        result.WorkOrderId.ShouldBe(wo.Id);
        result.SuggestedPurpose.ShouldBe(StockEntryType.MaterialTransferForManufacture.ToString());
        result.Items.Count.ShouldBe(1);
        result.Items[0].JobCardItemId.ShouldBe(wo.RequiredItems[0].Id);
    }

    [Fact]
    public async Task GetItemsFromMaterialRequestAsync_WithWorkOrderOnly_SetsFgCompletedQtyToZero()
    {
        // Regression test for ERPNext PR #60246 (commit c18d8a4237):
        // When Material Request has only WorkOrderId (no JobCardId), FgCompletedQty is reset to 0
        var (company, fgItem, rmItem, wo, jc, sourceWh, wipWh) = await SeedAsync("MR_WO", forQty: 12m, requiredQty: 24m);
        var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();

        var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-WO-001", MaterialRequestType.MaterialTransfer, DateTime.Today)
        {
            WorkOrderId = wo.Id,
            TargetWarehouseId = wipWh,
        };
        mr.AddItem(rmItem.Id, rmItem.ItemName, 24m, "Unit", warehouseId: wipWh);
        mr.Submit();
        await mrRepo.InsertAsync(mr, autoSave: true);

        var result = await stockEntryAppService.GetItemsFromMaterialRequestAsync(mr.Id);

        result.FgCompletedQty.ShouldBe(0m);
        result.JobCardId.ShouldBeNull();
        result.WorkOrderId.ShouldBe(wo.Id);
        result.SuggestedPurpose.ShouldBe(StockEntryType.MaterialTransferForManufacture.ToString());
    }

    [Fact]
    public async Task PostAndCancelAsync_WithJobCard_UpdatesAndReversesJobCardTransferredQty()
    {
        var (company, fgItem, rmItem, wo, jc, sourceWh, wipWh) = await SeedAsync("POST_JC", forQty: 10m, requiredQty: 20m);
        var stockEntryAppService = GetRequiredService<IStockEntryAppService>();
        var jcRepo = GetRequiredService<IRepository<JobCard, Guid>>();
        var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();

        // Seed FIFO queue so outward transfer can be posted and cancelled
        await sleRepo.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, rmItem.Id, sourceWh,
            DateTime.Today.AddDays(-1), quantityChange: 50m, valuationRate: 10m,
            balanceQuantity: 50m, balanceValue: 500m)
        {
            StockQueue = "[[50,10]]",
        }, autoSave: true);

        var entry = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
        {
            CompanyId = company.Id,
            EntryType = StockEntryType.MaterialTransferForManufacture,
            PostingDate = DateTime.Today,
            WorkOrderId = wo.Id,
            JobCardId = jc.Id,
            FgCompletedQty = 10m,
            Items =
            {
                new CreateStockEntryItemDto
                {
                    ItemId = rmItem.Id,
                    Quantity = 20m,
                    SourceWarehouseId = sourceWh,
                    TargetWarehouseId = wipWh,
                }
            }
        });

        await stockEntryAppService.SubmitAsync(entry.Id);
        await stockEntryAppService.PostAsync(entry.Id);

        var reloadedJc = await jcRepo.GetAsync(jc.Id);
        reloadedJc.TransferredQty.ShouldBe(10m);
        reloadedJc.Status.ShouldBe(JobCardStatus.MaterialTransferred);

        await stockEntryAppService.CancelAsync(entry.Id);

        var cancelledJc = await jcRepo.GetAsync(jc.Id);
        cancelledJc.TransferredQty.ShouldBe(0m);
        cancelledJc.Status.ShouldBe(JobCardStatus.Open);
    }

    private async Task<(Company Company, Item FgItem, Item RmItem, WorkOrder Wo, JobCard Jc, Guid SourceWh, Guid WipWh)> SeedAsync(string suffix, decimal forQty, decimal requiredQty)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
        var costCenterRepository = GetRequiredService<IRepository<Accounting.Entities.CostCenter, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<Accounting.Entities.FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
        var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
        var jcRepository = GetRequiredService<IRepository<JobCard, Guid>>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"JC Transfer Co {suffix}"), autoSave: true);
        var fgItem = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"FG-{suffix}", $"Finished Good {suffix}", ItemType.Goods), autoSave: true);
        var rmItem = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"RM-{suffix}", $"Raw Material {suffix}", ItemType.Goods), autoSave: true);

        var sourceWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"Source WH {suffix}"), autoSave: true);
        var wipWarehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, $"WIP WH {suffix}"), autoSave: true);

        var stockAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"11{suffix}", "Stock In Hand", Accounting.AccountType.Asset), autoSave: true);
        var expenseAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"51{suffix}", "Cost of Goods Sold", Accounting.AccountType.Expense), autoSave: true);
        var adjustmentAccount = await accountRepository.InsertAsync(
            new Accounting.Entities.Account(Guid.NewGuid(), company.Id, $"12{suffix}", "Stock Adjustment", Accounting.AccountType.Equity), autoSave: true);
        var costCenter = await costCenterRepository.InsertAsync(
            new Accounting.Entities.CostCenter(Guid.NewGuid(), company.Id, $"CC {suffix}"), autoSave: true);

        company.DefaultInventoryAccountId = stockAccount.Id;
        company.DefaultExpenseAccountId = expenseAccount.Id;
        company.DefaultStockAdjustmentAccountId = adjustmentAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new Accounting.Entities.FiscalYear(Guid.NewGuid(), company.Id, $"FY-{suffix}", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);

        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"SE Series {suffix}", "StockEntry", $"SE{suffix}-"), autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"MR Series {suffix}", "MR", $"MR{suffix}-"), autoSave: true);

        var bom = await bomRepository.InsertAsync(
            new BillOfMaterials(Guid.NewGuid(), company.Id, $"BOM-{suffix}", fgItem.Id) { Quantity = 1, IsActive = true }, autoSave: true);

        var wo = new WorkOrder(Guid.NewGuid(), company.Id, $"WO-{suffix}", fgItem.Id, bom.Id, quantity: forQty)
        {
            WipWarehouseId = wipWarehouse.Id,
        };
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, rmItem.ItemName, requiredQty)
        {
            SourceWarehouseId = sourceWarehouse.Id,
        });
        wo.Submit();
        await woRepository.InsertAsync(wo, autoSave: true);

        var jc = new JobCard(Guid.NewGuid(), company.Id, wo.Id, Guid.NewGuid(), forQuantity: forQty, sequenceId: 1)
        {
            WipWarehouseId = wipWarehouse.Id,
        };
        await jcRepository.InsertAsync(jc, autoSave: true);

        return (company, fgItem, rmItem, wo, jc, sourceWarehouse.Id, wipWarehouse.Id);
    }
}
