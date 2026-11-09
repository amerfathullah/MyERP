using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 18:
/// 1) Product Bundle delivers and returns components at the ordered packed qty despite subsequent component UOM factor changes (PR #65005, commits ea7e451bcf, 710a59f031, f0243bace9).
/// 2) Party Account validation enforces billing currency account consistency (PR #64936, commit 9ecfac159f).
/// </summary>
public class UpstreamBatch18ParityTests
{
    // === PR #65005 / commits ea7e451bcf, 710a59f031: Product Bundle Ordered Component Qty Retention ===

    [Fact]
    public async Task ProductBundleDecompositionService_DecomposeAsync_PreservesOrderedComponentQtyPerUnit_710a59f031()
    {
        var bundleId = Guid.NewGuid();
        var bundleItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();

        var bundle = new ProductBundle(bundleId, bundleItemId);
        bundle.AddItem(componentItemId, 1m, "Component Item", "Box");

        var bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);

        // Ordered ratio from Sales Order was 10 Nos per Box (stored in componentStockQtyPerUnit)
        var orderedRatios = new Dictionary<Guid, decimal>
        {
            [componentItemId] = 10m
        };

        // Decompose 2 units of bundle
        var decomposed = await service.DecomposeAsync(
            itemId: bundleItemId,
            transactionQty: 2m,
            parentRate: 100m,
            componentStockQtyPerUnit: orderedRatios);

        decomposed.Count.ShouldBe(1);
        // Scaled qty must equal 2 * 10 = 20 (NOT live item UOM factor)
        decomposed[0].Qty.ShouldBe(20m);
    }

    [Fact]
    public async Task ProductBundleDecompositionService_DecomposeTransactionItemsAsync_PassesOrderedRatioCorrectly_ea7e451bcf()
    {
        var bundleId = Guid.NewGuid();
        var bundleItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();

        var bundle = new ProductBundle(bundleId, bundleItemId);
        bundle.AddItem(componentItemId, 1m, "Component Item 2", "Box");

        var bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var service = new ProductBundleDecompositionService(bundleRepo);

        var orderedRatios = new Dictionary<Guid, decimal>
        {
            [componentItemId] = 10m
        };

        var transactionItems = new List<BundleTransactionItem>
        {
            new BundleTransactionItem(
                ItemId: bundleItemId,
                Qty: -2m, // Return of 2 units
                Rate: 100m,
                ComponentStockQtyPerUnit: orderedRatios)
        };

        var result = await service.DecomposeTransactionItemsAsync(transactionItems, isReturn: true);

        result.PackedItems.Count.ShouldBe(1);
        // Return qty must be -20
        result.PackedItems[0].Qty.ShouldBe(-20m);
    }

    // === PR #64936 / commit 9ecfac159f: Party Billing Currency Account Filtering & Invariants ===

    [Fact]
    public void PartyValidationService_ValidatePartyAccountCurrency_AcceptsBillingCurrencyAccount()
    {
        var service = new PartyValidationService();

        // Customer billing currency USD, Company currency MYR, Party Account currency USD -> Valid
        Should.NotThrow(() => service.ValidatePartyAccountCurrency(
            partyAccountCurrency: "USD",
            advanceAccountCurrency: "USD",
            partyBillingCurrency: "USD",
            companyCurrency: "MYR"));
    }

    [Fact]
    public void PartyValidationService_ValidatePartyAccountCurrency_RejectsForeignAccountWhenMismatched()
    {
        var service = new PartyValidationService();

        // Customer billing currency USD, Company currency MYR, Party Account currency EUR -> Rejects
        var ex = Should.Throw<BusinessException>(() => service.ValidatePartyAccountCurrency(
            partyAccountCurrency: "EUR",
            advanceAccountCurrency: null,
            partyBillingCurrency: "USD",
            companyCurrency: "MYR"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.PartyAccountCurrencyMismatch);
    }
}
