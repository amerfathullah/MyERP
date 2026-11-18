using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Projects;

public abstract class ProjectWiseStockTrackingReportTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IProjectAppService _projectAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _prRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _piRepository;
    private readonly IRepository<StockEntry, Guid> _seRepository;
    private readonly IRepository<SalesOrder, Guid> _soRepository;
    private readonly IRepository<DeliveryNote, Guid> _dnRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;

    protected ProjectWiseStockTrackingReportTests()
    {
        _projectAppService = GetRequiredService<IProjectAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _projectRepository = GetRequiredService<IRepository<Project, Guid>>();
        _poRepository = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
        _prRepository = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
        _piRepository = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        _seRepository = GetRequiredService<IRepository<StockEntry, Guid>>();
        _soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
        _dnRepository = GetRequiredService<IRepository<DeliveryNote, Guid>>();
        _sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
    }

    [Fact]
    public async Task Should_Aggregate_Purchased_Issued_And_Delivered_Costs_Per_Project()
    {
        var setup = await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "StockTrack Sdn Bhd"), true);
            var customer = await _customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Apex Buyer"), true);
            var supplier = await _supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Supply Partner"), true);
            var warehouse = await _warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main WH"), true);
            var item = await _itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITM-001", "Server Rack", ItemType.Goods), true);

            var project = new Project(Guid.NewGuid(), company.Id, "PROJ-STK-1", "Data Center Build")
            {
                CustomerId = customer.Id,
                EstimatedCost = 5000m,
            };
            await _projectRepository.InsertAsync(project, true);

            var date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

            // 1. Purchased via Purchase Receipt linked to PO item (2 * 250 = 500)
            var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-STK-1", date)
            {
                ProjectId = project.Id
            };
            var poItem = po.AddItem(item.Id, "Server Rack", 2m, 250m, 0m, projectId: project.Id);
            po.Submit();
            await _poRepository.InsertAsync(po, true);

            var pr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, warehouse.Id, "PR-STK-1", date);
            pr.PurchaseOrderId = po.Id;
            pr.AddItem(item.Id, "Server Rack", 2m, 250m, 0m, purchaseOrderItemId: poItem.Id);
            pr.Submit();
            await _prRepository.InsertAsync(pr, true);

            // 2. Purchased directly via Purchase Invoice with UpdateStock=true (1 * 150 = 150)
            var pi = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PI-STK-1", date)
            {
                ProjectId = project.Id,
                UpdateStock = true,
            };
            pi.AddItem(item.Id, "Cable Kit", 1m, 150m, 0m);
            pi.Submit();
            await _piRepository.InsertAsync(pi, true);

            // 3. Issued via Stock Entry Material Issue (TargetWarehouseId == null: 3 * 100 = 300)
            var se = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.MaterialIssue, date)
            {
                ProjectId = project.Id
            };
            se.AddItem(item.Id, 3m, sourceWarehouseId: warehouse.Id, targetWarehouseId: null, valuationRate: 100m);
            se.Submit();
            await _seRepository.InsertAsync(se, true);

            // 4. Delivered via Delivery Note linked to Sales Order (StockValueDifference = -220 => delivered cost = 220)
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-STK-1", date)
            {
                ProjectId = project.Id
            };
            so.AddItem(item.Id, "Server Rack", 1m, 400m, 0m);
            var soItemId = so.Items[0].Id;
            so.Submit();
            await _soRepository.InsertAsync(so, true);

            var dn = new DeliveryNote(Guid.NewGuid(), company.Id, customer.Id, warehouse.Id, "DN-STK-1", date)
            {
                SalesOrderId = so.Id
            };
            dn.AddItem(item.Id, "Server Rack", 1m, 400m, 0m, salesOrderItemId: soItemId);
            var dnItemId = dn.Items[0].Id;
            dn.Submit();
            await _dnRepository.InsertAsync(dn, true);

            await _sleRepository.InsertAsync(
                new StockLedgerEntry(
                    Guid.NewGuid(), company.Id, item.Id, warehouse.Id, date, date.TimeOfDay,
                    quantityChange: -1m, valuationRate: 220m, balanceQuantity: 1m, balanceValue: 220m,
                    voucherType: "DeliveryNote", voucherId: dn.Id, voucherDetailNo: dnItemId),
                true);

            return new { CompanyId = company.Id, ProjectId = project.Id };
        });

        var report = await _projectAppService.GetProjectWiseStockTrackingAsync(
            new ProjectWiseStockTrackingRequestDto { CompanyId = setup.CompanyId });

        report.CurrencyCode.ShouldBe("MYR");
        report.Rows.Count.ShouldBe(1);
        report.TotalPurchasedCost.ShouldBe(650m);
        report.TotalIssuedCost.ShouldBe(300m);
        report.TotalDeliveredCost.ShouldBe(220m);
        report.TotalEstimatedCost.ShouldBe(5000m);

        var row = report.Rows[0];
        row.ProjectId.ShouldBe(setup.ProjectId);
        row.CustomerName.ShouldBe("Apex Buyer");
        row.CostOfPurchasedItems.ShouldBe(650m);
        row.CostOfIssuedItems.ShouldBe(300m);
        row.CostOfDeliveredItems.ShouldBe(220m);
        row.EstimatedCost.ShouldBe(5000m);
    }

    [Fact]
    public async Task Should_Throw_When_CompanyId_Empty()
    {
        await Should.ThrowAsync<BusinessException>(async () =>
            await _projectAppService.GetProjectWiseStockTrackingAsync(
                new ProjectWiseStockTrackingRequestDto { CompanyId = Guid.Empty }));
    }
}
