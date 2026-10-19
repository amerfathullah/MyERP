using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Tax.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Unit tests for Purchase Order combining rules on Purchase Receipt.
/// Per ERPNext PR #60510 (commit 77e0c43997 / 7fda3d0b4f):
/// "One Purchase Receipt carries one currency, one set of rate-based taxes and one header discount."
/// </summary>
public class PurchaseReceiptCombinableOrdersTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private static PurchaseOrder CreateOrder(string orderNumber, string currency = "MYR")
    {
        return new PurchaseOrder(Guid.NewGuid(), CompanyId, SupplierId, orderNumber, DateTime.UtcNow.Date.AddDays(-2))
        {
            CurrencyCode = currency
        };
    }

    private static PurchaseReceipt CreateReceipt(string currency = "MYR")
    {
        return new PurchaseReceipt(Guid.NewGuid(), CompanyId, SupplierId, WarehouseId, "PR-001", DateTime.UtcNow.Date)
        {
            CurrencyCode = currency
        };
    }

    [Fact]
    public async Task Combine_OrdersWithDifferentCurrencies_Throws()
    {
        var po1 = CreateOrder("PO-001", "MYR");
        var po2 = CreateOrder("PO-002", "USD");
        var pr = CreateReceipt("MYR");

        var manager = new DomainServices.PurchaseReceiptManager(null!, null!);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateCombinablePurchaseOrdersAsync(pr, new[] { po1, po2 }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotCombinePurchaseOrders);
        (ex.Data["reason"]?.ToString() ?? string.Empty).ShouldContain("Currencies");
    }

    [Fact]
    public async Task Combine_OrderCurrencyDifferentFromReceipt_Throws()
    {
        var po1 = CreateOrder("PO-001", "USD");
        var po2 = CreateOrder("PO-002", "USD");
        var pr = CreateReceipt("MYR");

        var manager = new DomainServices.PurchaseReceiptManager(null!, null!);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateCombinablePurchaseOrdersAsync(pr, new[] { po1, po2 }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotCombinePurchaseOrders);
    }

    [Fact]
    public async Task Combine_OrderWithHeaderDiscountAmount_Throws()
    {
        var po1 = CreateOrder("PO-001", "MYR");
        po1.DiscountAmount = 50m;
        var po2 = CreateOrder("PO-002", "MYR");
        var pr = CreateReceipt("MYR");

        var manager = new DomainServices.PurchaseReceiptManager(null!, null!);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateCombinablePurchaseOrdersAsync(pr, new[] { po1, po2 }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotCombinePurchaseOrders);
        (ex.Data["reason"]?.ToString() ?? string.Empty).ShouldContain("discount amount");
    }

    [Fact]
    public async Task Combine_OrdersWithDifferentPercentageDiscounts_Throws()
    {
        var po1 = CreateOrder("PO-001", "MYR");
        po1.AdditionalDiscountPercentage = 10m;
        po1.ApplyDiscountOn = "Net Total";

        var po2 = CreateOrder("PO-002", "MYR");
        po2.AdditionalDiscountPercentage = 5m;
        po2.ApplyDiscountOn = "Net Total";

        var pr = CreateReceipt("MYR");
        var manager = new DomainServices.PurchaseReceiptManager(null!, null!);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateCombinablePurchaseOrdersAsync(pr, new[] { po1, po2 }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotCombinePurchaseOrders);
    }

    [Fact]
    public async Task Combine_OrdersWithSamePercentageDiscounts_Succeeds()
    {
        var po1 = CreateOrder("PO-001", "MYR");
        po1.AdditionalDiscountPercentage = 10m;
        po1.ApplyDiscountOn = "Net Total";

        var po2 = CreateOrder("PO-002", "MYR");
        po2.AdditionalDiscountPercentage = 10m;
        po2.ApplyDiscountOn = "Net Total";

        var pr = CreateReceipt("MYR");
        var manager = new DomainServices.PurchaseReceiptManager(null!, null!);

        // Should not throw
        await manager.ValidateCombinablePurchaseOrdersAsync(pr, new[] { po1, po2 });
    }

    [Fact]
    public async Task Combine_OrdersWithDifferentTaxes_Throws()
    {
        var po1 = CreateOrder("PO-001", "MYR");
        var po2 = CreateOrder("PO-002", "MYR");
        var pr = CreateReceipt("MYR");

        var tax1 = new TransactionTaxRow(Guid.NewGuid(), "PurchaseOrder", po1.Id, 1, "SST 8%", "On Net Total", 8m)
        {
            AccountId = Guid.NewGuid(),
            CostCenterId = Guid.NewGuid()
        };

        var tax2 = new TransactionTaxRow(Guid.NewGuid(), "PurchaseOrder", po2.Id, 1, "SST 10%", "On Net Total", 10m)
        {
            AccountId = tax1.AccountId,
            CostCenterId = tax1.CostCenterId
        };

        var taxRepo = Substitute.For<IRepository<TransactionTaxRow, Guid>>();
        taxRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { tax1, tax2 }.AsQueryable()));

        var manager = new DomainServices.PurchaseReceiptManager(null!, null!, taxRepo);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateCombinablePurchaseOrdersAsync(pr, new[] { po1, po2 }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotCombinePurchaseOrders);
        (ex.Data["reason"]?.ToString() ?? string.Empty).ShouldContain("taxes");
    }

    [Fact]
    public async Task Combine_OrdersWithIdenticalTaxes_Succeeds()
    {
        var po1 = CreateOrder("PO-001", "MYR");
        var po2 = CreateOrder("PO-002", "MYR");
        var pr = CreateReceipt("MYR");

        var accountId = Guid.NewGuid();
        var costCenterId = Guid.NewGuid();

        var tax1 = new TransactionTaxRow(Guid.NewGuid(), "PurchaseOrder", po1.Id, 1, "SST 8%", "On Net Total", 8m)
        {
            AccountId = accountId,
            CostCenterId = costCenterId
        };

        var tax2 = new TransactionTaxRow(Guid.NewGuid(), "PurchaseOrder", po2.Id, 1, "SST 8%", "On Net Total", 8m)
        {
            AccountId = accountId,
            CostCenterId = costCenterId
        };

        var taxRepo = Substitute.For<IRepository<TransactionTaxRow, Guid>>();
        taxRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { tax1, tax2 }.AsQueryable()));

        var manager = new DomainServices.PurchaseReceiptManager(null!, null!, taxRepo);

        // Should not throw
        await manager.ValidateCombinablePurchaseOrdersAsync(pr, new[] { po1, po2 });
    }
}
