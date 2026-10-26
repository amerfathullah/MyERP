using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

public class UpstreamParityBatch20261026Part2Tests
{
    [Fact]
    public void SalesPartner_AddTarget_RejectsNegativeQtyOrAmount()
    {
        // Per ERPNext PR #62324 (commit 16e6c794b5): reject negative sales targets
        var partner = new SalesPartner(Guid.NewGuid(), "Partner Alpha", PartnerType.Reseller, 5m);
        var fyId = Guid.NewGuid();

        var exQty = Should.Throw<BusinessException>(() =>
            partner.AddTarget(fyId, -5m, 1000m));
        exQty.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (exQty.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("cannot be negative");

        var exAmt = Should.Throw<BusinessException>(() =>
            partner.AddTarget(fyId, 10m, -100m));
        exAmt.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (exAmt.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("cannot be negative");
    }

    [Fact]
    public void SalesPartner_AddTarget_RejectsZeroQtyAndZeroAmount()
    {
        var partner = new SalesPartner(Guid.NewGuid(), "Partner Alpha", PartnerType.Reseller, 5m);
        var fyId = Guid.NewGuid();

        var ex = Should.Throw<BusinessException>(() =>
            partner.AddTarget(fyId, 0m, 0m));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("greater than zero");
    }

    [Fact]
    public void SalesPartner_AddTarget_RejectsDuplicateFiscalYearAndItemGroup()
    {
        // Per ERPNext PR #62324 (commit eec3bcf900): duplicate target row rejection
        var partner = new SalesPartner(Guid.NewGuid(), "Partner Alpha", PartnerType.Reseller, 5m);
        var fyId = Guid.NewGuid();
        var igId = Guid.NewGuid();

        partner.AddTarget(fyId, 10m, 1000m, igId);
        partner.Targets.Count.ShouldBe(1);

        var ex = Should.Throw<BusinessException>(() =>
            partner.AddTarget(fyId, 20m, 2000m, igId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("A target already exists for this fiscal year and item group");
    }

    [Fact]
    public void SalesPartner_RemoveTarget_RemovesTargetSuccessfully()
    {
        var partner = new SalesPartner(Guid.NewGuid(), "Partner Alpha", PartnerType.Reseller, 5m);
        var fyId = Guid.NewGuid();

        partner.AddTarget(fyId, 10m, 1000m);
        partner.Targets.Count.ShouldBe(1);

        var targetId = partner.Targets.First().Id;
        partner.RemoveTarget(targetId);
        partner.Targets.Count.ShouldBe(0);
    }

    [Fact]
    public void Asset_PurchaseRowItemIds_CanBeSetAndPreserved()
    {
        // Per ERPNext PR #62091 (commit f8de444ca5): preserve purchase row item links
        var asset = new Asset(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "AST-0001",
            "Server Rack",
            DateTime.UtcNow,
            5000m);

        asset.PurchaseReceiptItemId.ShouldBeNull();
        asset.PurchaseInvoiceItemId.ShouldBeNull();

        var prItemId = Guid.NewGuid();
        var piItemId = Guid.NewGuid();

        asset.PurchaseReceiptItemId = prItemId;
        asset.PurchaseInvoiceItemId = piItemId;

        asset.PurchaseReceiptItemId.ShouldBe(prItemId);
        asset.PurchaseInvoiceItemId.ShouldBe(piItemId);
    }

    [Fact]
    public async Task SalesPartnerTargetVariance_CalculatesAchievedAndVariance_RespectsCompany()
    {
        // Per ERPNext PR #62324 (commits 7d493a6678, eec3bcf900, a5f27756a9)
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var partner = new SalesPartner(partnerId, "Global Distributor", PartnerType.Distributor, 10m);
        partner.AddTarget(null, 100m, 50000m, itemGroupId);

        var partnerRepo = Substitute.For<IRepository<SalesPartner, Guid>>();
        partnerRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesPartner, object>>[]>())
            .Returns(Task.FromResult(new List<SalesPartner> { partner }.AsQueryable()));

        var company = new Company(companyId, "My Company");
        company.CurrencyCode = "MYR";
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.FindAsync(companyId).Returns(Task.FromResult<Company?>(company));

        var itemGroup = new ItemGroup(itemGroupId, "Electronics");
        var itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        itemGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemGroup> { itemGroup }.AsQueryable()));

        var item = new Item(itemId, companyId, "LAPTOP", "Laptop Core i7", ItemType.Goods);
        item.ItemGroupId = itemGroupId;
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        // Invoice 1: for companyId -> Achieved 30,000
        var invoice1 = new SalesInvoice(Guid.NewGuid(), companyId, Guid.NewGuid(), "INV-001", DateTime.UtcNow);
        invoice1.SalesPartnerId = partnerId;
        invoice1.AddItem(itemId, "Laptop Core i7", 15m, 2000m, 0m);
        invoice1.Submit();

        // Invoice 2: for otherCompanyId -> must be EXCLUDED per PR #62324 commit 7d493a6678
        var invoice2 = new SalesInvoice(Guid.NewGuid(), otherCompanyId, Guid.NewGuid(), "INV-002", DateTime.UtcNow);
        invoice2.SalesPartnerId = partnerId;
        invoice2.AddItem(itemId, "Laptop Core i7", 10m, 2000m, 0m);
        invoice2.Submit();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        invoiceRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { invoice1, invoice2 }.AsQueryable()));

        var orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        orderRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));

        var fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();

        var service = new SalesPartnerTargetVarianceAppService(
            partnerRepo,
            invoiceRepo,
            orderRepo,
            itemRepo,
            itemGroupRepo,
            fiscalYearRepo,
            companyRepo);

        var report = await service.GetReportAsync(new GetSalesPartnerTargetVarianceInput
        {
            CompanyId = companyId,
            DocType = "Sales Invoice",
            TargetOn = "Amount",
            SalesPartnerId = partnerId
        });

        report.ShouldNotBeNull();
        report.Rows.Count.ShouldBe(1);

        var row = report.Rows[0];
        row.SalesPartnerName.ShouldBe("Global Distributor");
        row.Target.ShouldBe(50000m);
        // Only invoice1 (30,000) should be included, other company (20,000) excluded
        row.Achieved.ShouldBe(30000m);
        row.Variance.ShouldBe(-20000m);
        row.AchievementPercentage.ShouldBe(60m);
    }
}
