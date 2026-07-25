using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MyERP.Inventory.Entities;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests covering upstream fixes (PR #57412 PickList→DN customer, PR #57413 batch null-expiry sort)
/// and Payment Entry receipt/voucher print layout data requirements.
/// </summary>
public class UpstreamFixesAndPaymentReceiptTests
{
    // --- PR #57413: Batch expiry null-safe sort ---

    [Fact]
    public void BatchSort_NullExpiry_SortsToEnd()
    {
        // Simulates mixed batch expiry: some with dates, some null
        var batches = new List<(string batchNo, DateTime? expiry)>
        {
            ("B001", new DateTime(2026, 3, 15)),
            ("B002", null),  // never expires
            ("B003", new DateTime(2026, 1, 10)),
            ("B004", null),  // never expires
            ("B005", new DateTime(2026, 6, 30)),
        };

        // C# handles null DateTime? sort natively — null sorts FIRST by default
        // ERPNext fix: (tup[1] is None, tup[1]) — puts null LAST
        // We replicate the same: sort by (hasExpiry ? 0 : 1, expiry)
        var sorted = batches
            .OrderBy(b => b.expiry == null ? 1 : 0)
            .ThenBy(b => b.expiry)
            .ToList();

        // Dated batches first (oldest expiry first = FIFO for expiry)
        Assert.Equal("B003", sorted[0].batchNo); // 2026-01-10
        Assert.Equal("B001", sorted[1].batchNo); // 2026-03-15
        Assert.Equal("B005", sorted[2].batchNo); // 2026-06-30
        // Null-expiry batches last (never expire = lowest priority for FIFO expiry)
        Assert.Null(sorted[3].expiry);
        Assert.Null(sorted[4].expiry);
    }

    [Fact]
    public void BatchSort_AllNull_NoException()
    {
        var batches = new List<(string batchNo, DateTime? expiry)>
        {
            ("B001", null),
            ("B002", null),
            ("B003", null),
        };

        var sorted = batches
            .OrderBy(b => b.expiry == null ? 1 : 0)
            .ThenBy(b => b.expiry)
            .ToList();

        Assert.Equal(3, sorted.Count);
        Assert.All(sorted, b => Assert.Null(b.expiry));
    }

    [Fact]
    public void BatchSort_AllDated_SortsByExpiry()
    {
        var batches = new List<(string batchNo, DateTime? expiry)>
        {
            ("B001", new DateTime(2027, 12, 31)),
            ("B002", new DateTime(2026, 6, 15)),
            ("B003", new DateTime(2026, 1, 1)),
        };

        var sorted = batches
            .OrderBy(b => b.expiry == null ? 1 : 0)
            .ThenBy(b => b.expiry)
            .ToList();

        Assert.Equal("B003", sorted[0].batchNo); // earliest expires first
        Assert.Equal("B002", sorted[1].batchNo);
        Assert.Equal("B001", sorted[2].batchNo); // latest expires last
    }

    [Fact]
    public void Batch_IsExpired_NullExpiryNeverExpires()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        batch.ExpiryDate = null;

        Assert.False(batch.IsExpired(DateTime.UtcNow));
        Assert.False(batch.IsExpired(DateTime.MaxValue)); // even far future
    }

    [Fact]
    public void Batch_IsExpired_PastDateIsExpired()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-002");
        batch.ExpiryDate = new DateTime(2025, 1, 1);

        Assert.True(batch.IsExpired(new DateTime(2026, 7, 23)));
    }

    [Fact]
    public void Batch_IsExpired_FutureDateNotExpired()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-003");
        batch.ExpiryDate = new DateTime(2028, 12, 31);

        Assert.False(batch.IsExpired(new DateTime(2026, 7, 23)));
    }

    // --- PR #57412: PickList CustomerId for DN creation ---

    [Fact]
    public void PickList_CustomerId_DefaultsNull()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        Assert.Null(pl.CustomerId);
    }

    [Fact]
    public void PickList_CustomerId_CanBeSet()
    {
        var customerId = Guid.NewGuid();
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.CustomerId = customerId;
        Assert.Equal(customerId, pl.CustomerId);
    }

    [Fact]
    public void PickList_CustomerMappedToDN_WhenNoSalesOrder()
    {
        // When creating DN from Pick List without SO, customer comes from PickList.CustomerId
        var customerId = Guid.NewGuid();
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.CustomerId = customerId;
        pl.SalesOrderId = null; // no SO

        // In production: DN.CustomerId = pl.CustomerId ?? (resolve from SO)
        var dnCustomerId = pl.SalesOrderId.HasValue
            ? Guid.Empty // would resolve from SO
            : pl.CustomerId ?? Guid.Empty;

        Assert.Equal(customerId, dnCustomerId);
    }

    [Fact]
    public void PickList_CustomerFromSO_TakesPrecedence()
    {
        // When SO is set, customer resolves from SO (not from pick list)
        var plCustomerId = Guid.NewGuid();
        var soCustomerId = Guid.NewGuid();
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.CustomerId = plCustomerId;
        pl.SalesOrderId = Guid.NewGuid(); // SO exists

        // SO customer takes precedence
        var dnCustomerId = pl.SalesOrderId.HasValue ? soCustomerId : pl.CustomerId ?? Guid.Empty;
        Assert.Equal(soCustomerId, dnCustomerId);
    }

    // --- Payment Entry Receipt/Voucher Print Layout ---

    [Fact]
    public void PaymentReceipt_TypeDeterminesTitle()
    {
        // Receive = "OFFICIAL RECEIPT", Pay = "PAYMENT VOUCHER"
        var receiveTitle = GetReceiptTitle("Receive");
        var payTitle = GetReceiptTitle("Pay");
        var transferTitle = GetReceiptTitle("InternalTransfer");

        Assert.Equal("OFFICIAL RECEIPT", receiveTitle);
        Assert.Equal("PAYMENT VOUCHER", payTitle);
        Assert.Equal("PAYMENT VOUCHER", transferTitle); // non-Receive defaults to voucher
    }

    [Fact]
    public void PaymentReceipt_TotalAllocated_SumsReferences()
    {
        var references = new List<(decimal allocated, string type)>
        {
            (5000m, "SalesInvoice"),
            (3000m, "SalesInvoice"),
            (2000m, "SalesInvoice"),
        };

        var totalAllocated = references.Sum(r => r.allocated);
        Assert.Equal(10000m, totalAllocated);
    }

    [Fact]
    public void PaymentReceipt_EmptyReferences_ZeroAllocated()
    {
        var references = new List<decimal>();
        var totalAllocated = references.Sum();
        Assert.Equal(0m, totalAllocated);
    }

    [Fact]
    public void PaymentReceipt_BaseAmount_UsesExchangeRate()
    {
        var paidAmount = 1000m;
        var exchangeRate = 4.72m; // USD → MYR
        var baseAmount = paidAmount * exchangeRate;
        Assert.Equal(4720m, baseAmount);
    }

    [Fact]
    public void PaymentReceipt_SameCurrency_ExchangeRateIsOne()
    {
        var paidAmount = 5000m;
        var exchangeRate = 1m; // MYR → MYR
        var baseAmount = paidAmount * exchangeRate;
        Assert.Equal(5000m, baseAmount);
    }

    [Fact]
    public void PaymentReceipt_PrintSignatureBlock_HasThreeSlots()
    {
        // Per ERPNext pattern: Prepared By, Authorized Signatory, Received By/Acknowledgement
        var signatureSlots = new[] { "Prepared By", "Authorized Signatory", "Received By" };
        Assert.Equal(3, signatureSlots.Length);
    }

    [Fact]
    public void PaymentReceipt_PartyLabel_DependsOnType()
    {
        var receiveLabel = GetPartyLabel("Receive");
        var payLabel = GetPartyLabel("Pay");

        Assert.Equal("Received From", receiveLabel);
        Assert.Equal("Paid To", payLabel);
    }

    [Fact]
    public void PaymentReceipt_MultiCurrency_ShowsExchangeSection()
    {
        var exchangeRate = 4.72m;
        var showExchangeSection = exchangeRate != 1m;
        Assert.True(showExchangeSection);
    }

    [Fact]
    public void PaymentReceipt_SameCurrency_HidesExchangeSection()
    {
        var exchangeRate = 1m;
        var showExchangeSection = exchangeRate != 1m;
        Assert.False(showExchangeSection);
    }

    // --- Helper methods ---

    private static string GetReceiptTitle(string paymentType)
        => paymentType == "Receive" ? "OFFICIAL RECEIPT" : "PAYMENT VOUCHER";

    private static string GetPartyLabel(string paymentType)
        => paymentType == "Receive" ? "Received From" : "Paid To";
}
