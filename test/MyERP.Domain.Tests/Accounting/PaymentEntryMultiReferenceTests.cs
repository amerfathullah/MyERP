using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class PaymentEntryMultiReferenceTests
{
    [Fact]
    public void PaymentEntry_References_DefaultsEmpty()
    {
        var pe = CreatePE();
        pe.References.ShouldNotBeNull();
        pe.References.Count.ShouldBe(0);
    }

    [Fact]
    public void PaymentEntry_References_CanAddMultiple()
    {
        var pe = CreatePE();
        pe.References.Add(CreateRef(pe.Id, "SalesInvoice", 5000));
        pe.References.Add(CreateRef(pe.Id, "SalesInvoice", 3000));

        pe.References.Count.ShouldBe(2);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_WithReferences()
    {
        var pe = CreatePE(10000);
        pe.References.Add(CreateRef(pe.Id, "SalesInvoice", 6000));
        pe.References.Add(CreateRef(pe.Id, "SalesInvoice", 2500));

        // 10000 - 6000 - 2500 = 1500 unallocated
        pe.UnallocatedAmount.ShouldBe(1500m);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_FullyAllocated()
    {
        var pe = CreatePE(8000);
        pe.References.Add(CreateRef(pe.Id, "SalesInvoice", 5000));
        pe.References.Add(CreateRef(pe.Id, "PurchaseInvoice", 3000));

        pe.UnallocatedAmount.ShouldBe(0m);
    }

    [Fact]
    public void PaymentEntry_UnallocatedAmount_NoReferences_NoInvoice()
    {
        var pe = CreatePE(5000);
        // No references and no AgainstInvoiceId → fully unallocated (advance payment)
        pe.UnallocatedAmount.ShouldBe(5000m);
    }

    [Fact]
    public void PaymentEntryReference_ExchangeRate_DefaultsOne()
    {
        var refEntry = CreateRef(Guid.NewGuid(), "SalesInvoice", 1000);
        refEntry.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void PaymentEntryReference_ExchangeRate_CanBeSet()
    {
        var refEntry = CreateRef(Guid.NewGuid(), "SalesInvoice", 1000);
        refEntry.ExchangeRate = 4.5m;
        refEntry.ExchangeRate.ShouldBe(4.5m);
    }

    [Fact]
    public void PaymentEntry_MixedReferences_DifferentTypes()
    {
        var pe = CreatePE(15000);
        pe.References.Add(CreateRef(pe.Id, "SalesInvoice", 8000));
        pe.References.Add(CreateRef(pe.Id, "SalesOrder", 7000)); // advance against SO

        pe.References.Count.ShouldBe(2);
        pe.References.Sum(r => r.AllocatedAmount).ShouldBe(15000m);
        pe.UnallocatedAmount.ShouldBe(0m);
    }

    [Fact]
    public void DocumentSeries_GenerateNextNumberForFiscalYear_IncludesYear()
    {
        var series = new DocumentSeries(
            Guid.NewGuid(), Guid.NewGuid(), "SI Numbering", "SalesInvoice", "SI-");
        series.ResetOnFiscalYear = true;

        var number = series.GenerateNextNumberForFiscalYear(2026);
        number.ShouldBe("SI-2026-00001");
    }

    [Fact]
    public void DocumentSeries_GenerateNextNumberForFiscalYear_Increments()
    {
        var series = new DocumentSeries(
            Guid.NewGuid(), Guid.NewGuid(), "PO Numbering", "PurchaseOrder", "PO-");
        series.ResetOnFiscalYear = true;

        series.GenerateNextNumberForFiscalYear(2026).ShouldBe("PO-2026-00001");
        series.GenerateNextNumberForFiscalYear(2026).ShouldBe("PO-2026-00002");
        series.GenerateNextNumberForFiscalYear(2026).ShouldBe("PO-2026-00003");
    }

    [Fact]
    public void DocumentSeries_GenerateNextNumberForFiscalYear_ResetsOnNewYear()
    {
        var series = new DocumentSeries(
            Guid.NewGuid(), Guid.NewGuid(), "DN Numbering", "DeliveryNote", "DN-");
        series.ResetOnFiscalYear = true;

        series.GenerateNextNumberForFiscalYear(2025).ShouldBe("DN-2025-00001");
        series.GenerateNextNumberForFiscalYear(2025).ShouldBe("DN-2025-00002");

        // New fiscal year — counter resets
        series.GenerateNextNumberForFiscalYear(2026).ShouldBe("DN-2026-00001");
        series.GenerateNextNumberForFiscalYear(2026).ShouldBe("DN-2026-00002");
    }

    [Fact]
    public void DocumentSeries_GenerateNextNumberForFiscalYear_NoReset_FallsBack()
    {
        var series = new DocumentSeries(
            Guid.NewGuid(), Guid.NewGuid(), "JE Numbering", "JournalEntry", "JE-");
        series.ResetOnFiscalYear = false;

        // When ResetOnFiscalYear is false, falls back to standard generation (no year)
        var num = series.GenerateNextNumberForFiscalYear(2026);
        num.ShouldBe("JE-00001"); // no year in prefix
    }

    [Fact]
    public void DocumentSeries_StandardGeneration_NoYear()
    {
        var series = new DocumentSeries(
            Guid.NewGuid(), Guid.NewGuid(), "MR Numbering", "MaterialRequest", "MR-");
        series.GenerateNextNumber().ShouldBe("MR-00001");
        series.GenerateNextNumber().ShouldBe("MR-00002");
    }

    private static PaymentEntry CreatePE(decimal amount = 10000)
    {
        return new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(),
            MyERP.Accounting.PaymentType.Receive,
            DateTime.UtcNow, amount,
            Guid.NewGuid(), Guid.NewGuid());
    }

    private static PaymentEntryReference CreateRef(Guid peId, string type, decimal amount)
    {
        return new PaymentEntryReference(
            Guid.NewGuid(), peId, type, Guid.NewGuid(),
            totalAmount: amount, outstandingAmount: amount, allocatedAmount: amount);
    }
}
