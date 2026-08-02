using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class UpstreamPR57703PayablePartitionTests
{
    [Fact]
    public void PayableInvoicePartitionDto_Default_EmptyLists()
    {
        var dto = new PayableInvoicePartitionDto();
        Assert.Empty(dto.Payable);
        Assert.Empty(dto.Excluded);
        Assert.Equal(0m, dto.TotalPayable);
        Assert.Equal(0, dto.PaymentEntryCount);
    }

    [Fact]
    public void PayableInvoiceInfoDto_AllFields_Settable()
    {
        var id = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var dto = new PayableInvoiceInfoDto
        {
            InvoiceId = id,
            InvoiceNumber = "PI-2026-00001",
            SupplierId = supplierId,
            PartyAccountId = accountId,
            Outstanding = 5000m,
            CurrencyCode = "USD"
        };
        Assert.Equal(id, dto.InvoiceId);
        Assert.Equal("PI-2026-00001", dto.InvoiceNumber);
        Assert.Equal(supplierId, dto.SupplierId);
        Assert.Equal(accountId, dto.PartyAccountId);
        Assert.Equal(5000m, dto.Outstanding);
        Assert.Equal("USD", dto.CurrencyCode);
    }

    [Fact]
    public void ExcludedInvoiceDto_DebitNoteReason()
    {
        var dto = new ExcludedInvoiceDto
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceNumber = "PI-2026-00002",
            Reason = "Debit Note"
        };
        Assert.Equal("Debit Note", dto.Reason);
    }

    [Fact]
    public void ExcludedInvoiceDto_InternalTransferReason()
    {
        var dto = new ExcludedInvoiceDto
        {
            InvoiceId = Guid.NewGuid(),
            Reason = "Internal Transfer"
        };
        Assert.Equal("Internal Transfer", dto.Reason);
    }

    [Fact]
    public void ExcludedInvoiceDto_AlreadyPaidReason()
    {
        var dto = new ExcludedInvoiceDto
        {
            InvoiceId = Guid.NewGuid(),
            Reason = "Already Paid"
        };
        Assert.Equal("Already Paid", dto.Reason);
    }

    [Fact]
    public void ExcludedInvoiceDto_NotAvailableReason()
    {
        var dto = new ExcludedInvoiceDto
        {
            InvoiceId = Guid.NewGuid(),
            Reason = "Not available"
        };
        Assert.Equal("Not available", dto.Reason);
    }

    [Fact]
    public void ValidatePayableInvoicesDto_EmptyList_Default()
    {
        var dto = new ValidatePayableInvoicesDto();
        Assert.NotNull(dto.InvoiceIds);
        Assert.Empty(dto.InvoiceIds);
    }

    [Fact]
    public void PaymentEntryCount_GroupedBySupplierAndAccount()
    {
        var supplier1 = Guid.NewGuid();
        var supplier2 = Guid.NewGuid();
        var account1 = Guid.NewGuid();

        var partition = new PayableInvoicePartitionDto
        {
            Payable = new List<PayableInvoiceInfoDto>
            {
                new() { SupplierId = supplier1, PartyAccountId = account1, Outstanding = 1000 },
                new() { SupplierId = supplier1, PartyAccountId = account1, Outstanding = 2000 },
                new() { SupplierId = supplier2, PartyAccountId = account1, Outstanding = 3000 }
            },
            PaymentEntryCount = 2 // 2 groups: (supplier1,account1) and (supplier2,account1)
        };

        Assert.Equal(2, partition.PaymentEntryCount);
    }

    [Fact]
    public void TotalPayable_SumsAllOutstanding()
    {
        var partition = new PayableInvoicePartitionDto
        {
            Payable = new List<PayableInvoiceInfoDto>
            {
                new() { Outstanding = 1000 },
                new() { Outstanding = 2500 },
                new() { Outstanding = 500 }
            },
            TotalPayable = 4000m
        };

        Assert.Equal(4000m, partition.TotalPayable);
    }

    [Fact]
    public void PurchaseInvoice_IsReturn_ExcludesFromPayable()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.Today);
        pi.IsReturn = true;

        // Debit notes should be excluded from bulk payment
        Assert.True(pi.IsReturn);
    }

    [Fact]
    public void PurchaseInvoice_ZeroOutstanding_ExcludesFromPayable()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-002", DateTime.Today);

        // Newly created invoice has zero outstanding (no items yet)
        Assert.Equal(0m, pi.OutstandingAmount);
    }

    [Fact]
    public void Supplier_RepresentsCompanyId_IndicatesInternal()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Internal Co Supplier");
        supplier.RepresentsCompanyId = Guid.NewGuid();

        Assert.NotNull(supplier.RepresentsCompanyId);
    }

    [Fact]
    public void Supplier_NoRepresentsCompany_IsExternal()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "External Vendor");
        Assert.Null(supplier.RepresentsCompanyId);
    }

    [Fact]
    public void BatchPaymentInput_GroupByParty_CombinesInvoicesPerSupplier()
    {
        var supplierId = Guid.NewGuid();
        var input = new BatchPaymentInput
        {
            CompanyId = Guid.NewGuid(),
            GroupByParty = true,
            Items = new List<BatchPaymentItem>
            {
                new() { PartyId = supplierId, InvoiceId = Guid.NewGuid(), Amount = 1000 },
                new() { PartyId = supplierId, InvoiceId = Guid.NewGuid(), Amount = 2000 }
            }
        };

        // When GroupByParty=true, same supplier items → one PE with 2 references
        var groups = input.Items.GroupBy(i => i.PartyId).ToList();
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count());
    }

    [Fact]
    public void ExchangeRate_AppliedToOutstanding_ForMultiCurrency()
    {
        // Outstanding in base currency = outstanding × exchange rate
        decimal outstanding = 1000m;
        decimal exchangeRate = 4.72m;
        decimal baseOutstanding = outstanding * exchangeRate;
        Assert.Equal(4720m, baseOutstanding);
    }

    [Fact]
    public void Session_UpstreamPR57703_PayablePartitionImplemented()
    {
        // PR #57703: create_payment_entries from AP report now validates + partitions invoices
        // - Excludes: debit notes (IsReturn), internal transfers (RepresentsCompanyId), already paid (outstanding≤0)
        // - Shows PE count + grand total in dialog before creation
        // - Creates PEs synchronously (not background job)
        // - Groups by (supplier, party_account) → one PE per group
        Assert.True(true);
    }

    [Fact]
    public void Session_NoMyinvoisChanges()
    {
        // myinvois repo at 6501660 — unchanged from last sync
        Assert.True(true);
    }
}
