using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

public abstract class StockDiagnosticsTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IStockDiagnosticsAppService _diagnosticsService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<StockReconciliation, Guid> _reconRepository;

    protected StockDiagnosticsTests()
    {
        _diagnosticsService = GetRequiredService<IStockDiagnosticsAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
        _itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _sleRepository = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        _reconRepository = GetRequiredService<IRepository<StockReconciliation, Guid>>();
    }

    [Fact]
    public async Task IncorrectBalanceQty_Should_Detect_Corrupted_Balances_And_Reconciliation_Target()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Balance Diag Co"), autoSave: true);
        var warehouse = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "Main WH"), autoSave: true);
        var healthyItem = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "HEALTHY-1", "Healthy Item", ItemType.Goods), autoSave: true);
        var brokenItem = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "BROKEN-1", "Broken Item", ItemType.Goods), autoSave: true);
        var reconItem = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "RECON-1", "Recon Item", ItemType.Goods), autoSave: true);

        // Healthy sequence: +20 -> balance 20, -5 -> balance 15
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, healthyItem.Id, warehouse.Id,
            new DateTime(2026, 6, 1), 20m, 50m, 20m, 1000m), autoSave: true);
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, healthyItem.Id, warehouse.Id,
            new DateTime(2026, 6, 2), -5m, 50m, 15m, 750m), autoSave: true);

        // Tampered balance: +10 -> actual balance stored as 5
        var badSle = await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, brokenItem.Id, warehouse.Id,
            new DateTime(2026, 6, 1), 10m, 100m, 5m, 500m), autoSave: true);

        // Stock Reconciliation tampered balance when QuantityChange == 0 against NewQuantity == 7
        var recon = new StockReconciliation(Guid.NewGuid(), company.Id, new DateTime(2026, 6, 2))
        {
            ReconciliationNumber = "SR-001",
        };
        recon.AddItem(reconItem.Id, warehouse.Id, newQuantity: 7m, newValuationRate: 100m, currentQuantity: 10m, currentValuationRate: 100m);
        recon.Submit();
        await _reconRepository.InsertAsync(recon, autoSave: true);
        var reconRowId = recon.Items.Single().Id;

        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, reconItem.Id, warehouse.Id,
            new DateTime(2026, 6, 1), 10m, 100m, 10m, 1000m), autoSave: true);
        var tamperedReconSle = await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, reconItem.Id, warehouse.Id,
            new DateTime(2026, 6, 2), 0m, 100m, 9m, 900m)
        {
            VoucherType = "StockReconciliation",
            VoucherId = recon.Id,
            VoucherDetailNo = reconRowId,
        }, autoSave: true);

        var report = await _diagnosticsService.GetIncorrectBalanceQtyAsync(new IncorrectBalanceQtyRequestDto
        {
            CompanyId = company.Id,
            WarehouseId = warehouse.Id,
        });

        report.Rows.Select(r => r.ItemId).ShouldNotContain(healthyItem.Id);

        var brokenRow = report.Rows.Single(r => r.StockLedgerEntryId == badSle.Id);
        brokenRow.ExpectedBalanceQty.ShouldBe(10m);
        brokenRow.ActualBalanceQty.ShouldBe(5m);
        brokenRow.Difference.ShouldBe(5m);

        var reconReportRow = report.Rows.Single(r => r.StockLedgerEntryId == tamperedReconSle.Id);
        reconReportRow.VoucherNo.ShouldBe("SR-001");
        reconReportRow.ExpectedBalanceQty.ShouldBe(7m);
        reconReportRow.ActualBalanceQty.ShouldBe(9m);
        reconReportRow.Difference.ShouldBe(2m);
    }

    [Fact]
    public async Task IncorrectStockValue_Should_Validate_Perpetual_And_Scope_By_Account_Warehouses()
    {
        var nonPerpCompany = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Periodic Co") { EnablePerpetualInventory = false }, autoSave: true);
        await Should.ThrowAsync<BusinessException>(async () =>
            await _diagnosticsService.GetIncorrectStockValueAsync(new IncorrectStockValueRequestDto
            {
                CompanyId = nonPerpCompany.Id,
                AccountId = Guid.NewGuid(),
            }));

        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Perpetual Co") { EnablePerpetualInventory = true }, autoSave: true);

        await Should.ThrowAsync<BusinessException>(async () =>
            await _diagnosticsService.GetIncorrectStockValueAsync(new IncorrectStockValueRequestDto
            {
                CompanyId = company.Id,
                AccountId = Guid.Empty,
            }));

        var stockAcc1 = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1141", "Stock WH1", AccountType.Asset) { AccountSubType = AccountSubType.Stock }, autoSave: true);
        var stockAcc2 = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1142", "Stock WH2", AccountType.Asset) { AccountSubType = AccountSubType.Stock }, autoSave: true);

        var wh1 = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "WH 1") { DefaultAccountId = stockAcc1.Id }, autoSave: true);
        var wh2 = await _warehouseRepository.InsertAsync(
            new Warehouse(Guid.NewGuid(), company.Id, "WH 2") { DefaultAccountId = stockAcc2.Id }, autoSave: true);

        var item = await _itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "VAL-1", "Valuation Item", ItemType.Goods), autoSave: true);

        // WH1: opening +1000 on Feb 1, valid -200 on Mar 1 (BalanceValue = 800)
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, wh1.Id,
            new DateTime(2026, 2, 1), 10m, 100m, 10m, 1000m) { StockValueDifference = 1000m }, autoSave: true);
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, wh1.Id,
            new DateTime(2026, 3, 1), -2m, 100m, 8m, 800m) { StockValueDifference = -200m }, autoSave: true);

        // WH2: opening +1000 on Feb 1, tampered issue on Mar 1 (BalanceValue = 1300 instead of 800 -> diff 500)
        await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, wh2.Id,
            new DateTime(2026, 2, 1), 10m, 100m, 10m, 1000m) { StockValueDifference = 1000m }, autoSave: true);
        var tamperedWh2 = await _sleRepository.InsertAsync(new StockLedgerEntry(
            Guid.NewGuid(), company.Id, item.Id, wh2.Id,
            new DateTime(2026, 3, 1), -2m, 100m, 8m, 1300m) { StockValueDifference = -200m }, autoSave: true);

        var reportAcc1 = await _diagnosticsService.GetIncorrectStockValueAsync(new IncorrectStockValueRequestDto
        {
            CompanyId = company.Id,
            AccountId = stockAcc1.Id,
            FromDate = new DateTime(2026, 2, 15),
        });
        reportAcc1.Rows.ShouldBeEmpty();

        var reportAcc2 = await _diagnosticsService.GetIncorrectStockValueAsync(new IncorrectStockValueRequestDto
        {
            CompanyId = company.Id,
            AccountId = stockAcc2.Id,
            FromDate = new DateTime(2026, 2, 15),
        });
        reportAcc2.Rows.Count.ShouldBe(1);
        reportAcc2.Rows[0].StockLedgerEntryId.ShouldBe(tamperedWh2.Id);
        reportAcc2.Rows[0].ExpectedStockValue.ShouldBe(800m);
        reportAcc2.Rows[0].StockValue.ShouldBe(1300m);
        reportAcc2.Rows[0].DifferenceValue.ShouldBe(500m);
    }
}
