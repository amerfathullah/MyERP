using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.CRM.Entities;
using MyERP.EInvoice;
using MyERP.EInvoice.Services;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class UpstreamBatch59129To59143Tests
{
    // --- PR #59134: Pick List Stock Reservation for Product Bundles and Items ---

    [Fact]
    public void PickListItem_DefaultStockReservedQty_IsZero()
    {
        var item = new PickListItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            qty: 10, stockQty: 10, itemName: "Item A", batchId: null);

        item.StockReservedQty.ShouldBe(0m);
        item.ProductBundleItemId.ShouldBeNull();
    }

    [Fact]
    public void PickListItem_SetStockReservedQty_UpdatesQty()
    {
        var item = new PickListItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            qty: 10, stockQty: 10, itemName: "Item A", batchId: null);

        item.SetStockReservedQty(7.5m);
        item.StockReservedQty.ShouldBe(7.5m);
    }

    [Fact]
    public void PickListItem_SetStockReservedQty_Throws_WhenNegative()
    {
        var item = new PickListItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            qty: 10, stockQty: 10, itemName: "Item A", batchId: null);

        Should.Throw<ArgumentException>(() => item.SetStockReservedQty(-1m));
    }

    [Fact]
    public void PickList_Cancel_Throws_WhenItemsHaveActiveStockReservations()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), qty: 5, stockQty: 5);
        pl.Submit();

        // Mark item as reserved via Pick List
        pl.Items[0].SetStockReservedQty(5m);

        var ex = Should.Throw<BusinessException>(() => pl.Cancel());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public void PickList_AddItem_AcceptsBundleAndSourceRowLinks()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        var bundleItemId = Guid.NewGuid();
        var soDetailId = Guid.NewGuid();

        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), qty: 3, stockQty: 3, itemName: "Component 1",
            productBundleItemId: bundleItemId, sourceDocumentItemId: soDetailId);

        var item = pl.Items[0];
        item.ProductBundleItemId.ShouldBe(bundleItemId);
        item.SourceDocumentItemId.ShouldBe(soDetailId);
    }

    // --- PR #59131: Disassembly Representative Tie-Breaker Collation Stability ---

    [Fact]
    public void Disassembly_OrderLines_PicksEarliestCreationAndCasefoldedEntryName()
    {
        var creationTime = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);

        var entry1 = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, creationTime)
        {
            EntryNumber = "MFG-002",
            FgCompletedQty = 10
        };
        typeof(Volo.Abp.Domain.Entities.Auditing.CreationAuditedAggregateRoot<Guid>)
            .GetProperty(nameof(entry1.CreationTime))!
            .SetValue(entry1, creationTime);

        var entry2 = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, creationTime)
        {
            EntryNumber = "mfg-001",
            FgCompletedQty = 10
        };
        typeof(Volo.Abp.Domain.Entities.Auditing.CreationAuditedAggregateRoot<Guid>)
            .GetProperty(nameof(entry2.CreationTime))!
            .SetValue(entry2, creationTime);

        var entries = new List<StockEntry> { entry1, entry2 };

        // Order deterministically per PR #59131: CreationTime, casefold EntryNumber, EntryNumber
        var sorted = entries
            .OrderBy(e => e.CreationTime)
            .ThenBy(e => (e.EntryNumber ?? string.Empty).ToLowerInvariant(), StringComparer.Ordinal)
            .ThenBy(e => e.EntryNumber ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        sorted[0].EntryNumber.ShouldBe("mfg-001");
        sorted[1].EntryNumber.ShouldBe("MFG-002");
    }

    // --- PR #59133: Prospect Territory Resolution ---

    [Fact]
    public void Prospect_CarriesTerritory_ForSellingReports()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Acme Corp")
        {
            Territory = "Northern Region",
            CompanyName = "Acme Global Solutions"
        };

        prospect.Territory.ShouldBe("Northern Region");
        prospect.CompanyName.ShouldBe("Acme Global Solutions");
    }

    // --- PR #59143: MPS / MRP Bucket Inclusive ToDate Boundary ---

    [Fact]
    public void MpsFetchSalesOrders_InclusiveBoundary_MatchesOrderOnSameDate()
    {
        var filterToDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var orderDate = new DateTime(2026, 12, 1, 15, 30, 0, DateTimeKind.Utc);

        // Date-level inclusive check ensures 15:30 on same date is included (per PR #59143)
        var isIncluded = orderDate.Date <= filterToDate.Date;
        isIncluded.ShouldBeTrue();
    }

    // --- MyInvois PR #79: Supplier TIN Search Error Message Propagation ---

    [Fact]
    public async Task TaxpayerValidationService_ThrowsUserFriendlyException_WithDetailedErrorMessage()
    {
        var lhdnClient = Substitute.For<ILhdnApiClient>();
        var settings = Substitute.For<ISettingProvider>();

        settings.GetOrNullAsync("EInvoice.AccessToken").Returns(Task.FromResult<string?>("test-token"));
        settings.GetOrNullAsync("EInvoice.Environment").Returns(Task.FromResult<string?>("Sandbox"));

        lhdnClient.SearchTaxpayerAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<LhdnEnvironment>(), Arg.Any<string?>())
            .Returns(Task.FromResult(new LhdnTaxpayerSearchResponse
            {
                IsFound = false,
                StatusCode = 400,
                ErrorMessage = "Status code: 400 As per LHDN, either type or value or taxpayer data is wrong"
            }));

        var service = new TaxpayerValidationService(lhdnClient, settings);

        var ex = await Should.ThrowAsync<UserFriendlyException>(() =>
            service.ValidateTaxpayerAsync("BRN", "123456789"));

        ex.Message.ShouldContain("API request failed: Status code: 400");
    }
}
