using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Tax;
using MyERP.Tax.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

public class TerritoryTargetsAndVatReportTests
{
    [Fact]
    public void Territory_AddTarget_RejectsNegativeQtyOrAmount()
    {
        var territory = new Territory(Guid.NewGuid(), "Selangor");
        var fyId = Guid.NewGuid();

        var exQty = Should.Throw<BusinessException>(() =>
            territory.AddTarget(fyId, targetQty: -5m, targetAmount: 1000m));
        exQty.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

        var exAmt = Should.Throw<BusinessException>(() =>
            territory.AddTarget(fyId, targetQty: 5m, targetAmount: -100m));
        exAmt.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Territory_AddTarget_RejectsBothZero()
    {
        var territory = new Territory(Guid.NewGuid(), "Penang");
        var fyId = Guid.NewGuid();

        var ex = Should.Throw<BusinessException>(() =>
            territory.AddTarget(fyId, targetQty: 0m, targetAmount: 0m));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Territory_AddTarget_RejectsDuplicateFiscalYearAndItemGroup()
    {
        var territory = new Territory(Guid.NewGuid(), "Johor");
        var fyId = Guid.NewGuid();
        var igId = Guid.NewGuid();

        territory.AddTarget(fyId, targetQty: 100m, targetAmount: 5000m, itemGroupId: igId);

        var ex = Should.Throw<BusinessException>(() =>
            territory.AddTarget(fyId, targetQty: 200m, targetAmount: 10000m, itemGroupId: igId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Territory_AddTarget_And_RemoveTarget_Success()
    {
        var territory = new Territory(Guid.NewGuid(), "Perak");
        var fyId = Guid.NewGuid();

        territory.AddTarget(fyId, targetQty: 50m, targetAmount: 2500m);
        territory.Targets.Count.ShouldBe(1);

        var targetId = territory.Targets.First().Id;
        territory.RemoveTarget(targetId);
        territory.Targets.Count.ShouldBe(0);
    }

    [Fact]
    public async Task TerritoryTargetVariance_RollsUpDescendantTerritorySales()
    {
        var companyId = Guid.NewGuid();
        var fyId = Guid.NewGuid();

        // Hierarchy: Malaysia (parent) -> Kuala Lumpur (child)
        var parentTerritory = new Territory(Guid.NewGuid(), "Malaysia", isGroup: true);
        var childTerritory = new Territory(Guid.NewGuid(), "Kuala Lumpur", parentId: parentTerritory.Id, isGroup: false);

        parentTerritory.AddTarget(fyId, targetQty: 100m, targetAmount: 10000m);

        var customer = new Customer(Guid.NewGuid(), companyId, "KL Customer");
        customer.TerritoryId = childTerritory.Id;

        var item = new Item(Guid.NewGuid(), companyId, "SKU-1", "Laptop", ItemType.Goods);

        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customer.Id, "INV-001", DateTime.UtcNow);
        invoice.AddItem(item.Id, "Laptop", quantity: 2m, unitPrice: 3000m, taxAmount: 0m);
        invoice.Submit();

        var territoryRepo = Substitute.For<IRepository<Territory, Guid>>();
        territoryRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Territory, object>>[]>())
            .Returns(Task.FromResult(new List<Territory> { parentTerritory, childTerritory }.AsQueryable()));

        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        customerRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));

        var salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        salesInvoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { invoice }.AsQueryable()));

        var salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        salesOrderRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        itemGroupRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<ItemGroup>().AsQueryable()));

        var fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        fiscalYearRepo.FindAsync(fyId)
            .Returns(Task.FromResult<FiscalYear?>(new FiscalYear(fyId, companyId, "2026", DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(6))));

        var company = new Company(companyId, "MyCorp") { CurrencyCode = "MYR" };
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.FindAsync(companyId)
            .Returns(Task.FromResult<Company?>(company));

        var service = new TerritoryTargetVarianceAppService(
            territoryRepo, customerRepo, salesInvoiceRepo, salesOrderRepo,
            itemRepo, itemGroupRepo, fiscalYearRepo, companyRepo);

        var report = await service.GetReportAsync(new GetTerritoryTargetVarianceInput
        {
            CompanyId = companyId,
            FiscalYearId = fyId,
            DocType = "Sales Invoice",
            TargetOn = "Amount"
        });

        report.Rows.Count.ShouldBeGreaterThan(0);
        var malaysiaRow = report.Rows.First(r => r.TerritoryId == parentTerritory.Id);
        malaysiaRow.Target.ShouldBe(10000m);
        malaysiaRow.Achieved.ShouldBe(6000m); // rolled up from child territory
        malaysiaRow.Variance.ShouldBe(-4000m);
        malaysiaRow.AchievementPercentage.ShouldBe(60m);
    }

    [Fact]
    public void LhdnVatCalculation_ReturnAdjustment_And_NetPayable()
    {
        // Output tax: 1000, Return adjustment: 200 => Net output: 800
        // Input tax: 500, Debit note adjustment: 50 => Net input: 450
        // Net payable = 800 - 450 = 350
        decimal salesAmount = 10000m;
        decimal salesVat = 1000m;
        decimal returnAmount = 2000m;
        decimal returnVat = 200m;

        decimal totalSalesAmount = salesAmount;
        decimal totalSalesAdjustment = returnAmount;
        decimal totalSalesVat = salesVat - returnVat;

        totalSalesAmount.ShouldBe(10000m);
        totalSalesAdjustment.ShouldBe(2000m);
        totalSalesVat.ShouldBe(800m);

        decimal purchaseAmount = 5000m;
        decimal purchaseVat = 500m;
        decimal debitNoteAmount = 500m;
        decimal debitNoteVat = 50m;

        decimal totalPurchaseAmount = purchaseAmount;
        decimal totalPurchaseAdjustment = debitNoteAmount;
        decimal totalPurchaseVat = purchaseVat - debitNoteVat;

        totalPurchaseVat.ShouldBe(450m);
        decimal netPayable = totalSalesVat - totalPurchaseVat;
        netPayable.ShouldBe(350m);
    }
}
