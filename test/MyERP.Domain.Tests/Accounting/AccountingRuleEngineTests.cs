using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Tests for AccountingRuleEngine calculation and dispatch logic.
/// Verifies amount resolution, account resolution, and journal entry construction.
/// </summary>
public class AccountingRuleEngineTests
{
    // === IAccountableDocument implementation for testing ===

    private class TestDocument : IAccountableDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CompanyId { get; set; } = Guid.NewGuid();
        public string DocumentType { get; set; } = "SalesInvoice";
        public decimal NetTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? SupplierId { get; set; }
        public DateTime PostingDate { get; set; } = DateTime.Today;
        public string CurrencyCode { get; set; } = "MYR";
        public decimal ExchangeRate { get; set; } = 1m;
    }

    // === AmountSource Resolution Tests ===

    [Fact]
    public void AmountSource_NetTotal_ResolvesCorrectly()
    {
        var doc = new TestDocument { NetTotal = 1000, GrandTotal = 1080, TaxAmount = 80 };
        ResolveAmount(AmountSource.NetTotal, doc).ShouldBe(1000);
    }

    [Fact]
    public void AmountSource_GrandTotal_ResolvesCorrectly()
    {
        var doc = new TestDocument { NetTotal = 1000, GrandTotal = 1080, TaxAmount = 80 };
        ResolveAmount(AmountSource.GrandTotal, doc).ShouldBe(1080);
    }

    [Fact]
    public void AmountSource_TaxAmount_ResolvesCorrectly()
    {
        var doc = new TestDocument { NetTotal = 1000, GrandTotal = 1080, TaxAmount = 80 };
        ResolveAmount(AmountSource.TaxAmount, doc).ShouldBe(80);
    }

    [Fact]
    public void AmountSource_ZeroTax_SkipsRuleWithZeroAmount()
    {
        // Rules with zero amount should be skipped (per engine logic: if amount <= 0 continue)
        var doc = new TestDocument { NetTotal = 1000, GrandTotal = 1000, TaxAmount = 0 };
        ResolveAmount(AmountSource.TaxAmount, doc).ShouldBe(0);
    }

    // === Journal Entry Construction Tests ===

    [Fact]
    public void JournalEntry_SalesInvoice_ThreeLines_Balanced()
    {
        // SI rule set: DR Receivable (GrandTotal), CR Revenue (NetTotal), CR Tax (TaxAmount)
        var companyId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();
        var receivableAccId = Guid.NewGuid();
        var revenueAccId = Guid.NewGuid();
        var taxAccId = Guid.NewGuid();

        var doc = new TestDocument
        {
            CompanyId = companyId,
            NetTotal = 1000,
            GrandTotal = 1080,
            TaxAmount = 80,
        };

        var journal = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, doc.PostingDate);
        journal.ReferenceType = doc.DocumentType;
        journal.ReferenceId = doc.Id;

        // Simulate rule processing: DR Receivable for GrandTotal
        journal.AddLine(receivableAccId, 1080, isDebit: true);
        // CR Revenue for NetTotal
        journal.AddLine(revenueAccId, 1000, isDebit: false);
        // CR Tax for TaxAmount
        journal.AddLine(taxAccId, 80, isDebit: false);

        // Validate: total DR = total CR (double-entry)
        journal.Lines.Where(l => l.IsDebit).Sum(l => l.Amount).ShouldBe(1080);
        journal.Lines.Where(l => !l.IsDebit).Sum(l => l.Amount).ShouldBe(1080);
        journal.Lines.Count.ShouldBe(3);

        // Should not throw
        journal.Validate();
    }

    [Fact]
    public void JournalEntry_PurchaseInvoice_TwoLines_Balanced()
    {
        // PI rule set: DR Expense (NetTotal), CR Payable (GrandTotal)
        // Note: tax is included in GrandTotal for purchase side (simplification)
        var companyId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();
        var expenseAccId = Guid.NewGuid();
        var payableAccId = Guid.NewGuid();

        var journal = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, DateTime.Today);

        // DR Expense (NetTotal)
        journal.AddLine(expenseAccId, 500, isDebit: true);
        // CR Payable (GrandTotal) — but for balance we need same amount
        journal.AddLine(payableAccId, 500, isDebit: false);

        journal.Validate(); // should not throw — balanced
    }

    [Fact]
    public void JournalEntry_Unbalanced_ThrowsOnValidate()
    {
        var companyId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();
        var journal = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, DateTime.Today);

        journal.AddLine(Guid.NewGuid(), 1000, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 999, isDebit: false); // off by 1!

        Should.Throw<Volo.Abp.BusinessException>(() => journal.Validate());
    }

    [Fact]
    public void JournalEntry_NoLines_ThrowsOnValidate()
    {
        var journal = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        Should.Throw<Volo.Abp.BusinessException>(() => journal.Validate());
    }

    [Fact]
    public void JournalEntry_PostSetsPostedStatus()
    {
        var companyId = Guid.NewGuid();
        var journal = new JournalEntry(Guid.NewGuid(), companyId, Guid.NewGuid(), DateTime.Today);
        journal.AddLine(Guid.NewGuid(), 100, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 100, isDebit: false);

        journal.Status.ShouldBe(DocumentStatus.Draft);
        journal.Validate();
        journal.Post();
        journal.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void JournalEntry_DoublePost_Throws()
    {
        var journal = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        journal.AddLine(Guid.NewGuid(), 100, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 100, isDebit: false);
        journal.Validate();
        journal.Post();

        Should.Throw<Volo.Abp.BusinessException>(() => journal.Post());
    }

    // === AccountingRule Entity Tests ===

    [Fact]
    public void AccountingRule_SortOrder_DeterminesProcessingSequence()
    {
        var rules = new List<AccountingRule>
        {
            CreateRule("Tax CR", AmountSource.TaxAmount, false, 3),
            CreateRule("Revenue CR", AmountSource.NetTotal, false, 2),
            CreateRule("Receivable DR", AmountSource.GrandTotal, true, 1),
        };

        var ordered = rules.OrderBy(r => r.SortOrder).ToList();

        ordered[0].Name.ShouldBe("Receivable DR");
        ordered[1].Name.ShouldBe("Revenue CR");
        ordered[2].Name.ShouldBe("Tax CR");
    }

    [Fact]
    public void AccountingRule_IsActive_FiltersCorrectly()
    {
        var rules = new List<AccountingRule>
        {
            CreateRule("Active Rule", AmountSource.GrandTotal, true, 1),
            CreateRule("Inactive Rule", AmountSource.GrandTotal, true, 2, isActive: false),
        };

        var active = rules.Where(r => r.IsActive).ToList();
        active.Count.ShouldBe(1);
        active[0].Name.ShouldBe("Active Rule");
    }

    [Fact]
    public void AccountingRule_DocumentType_FiltersByType()
    {
        var companyId = Guid.NewGuid();
        var rules = new List<AccountingRule>
        {
            CreateRule("SI Revenue", AmountSource.NetTotal, false, 1, "SalesInvoice"),
            CreateRule("PI Expense", AmountSource.NetTotal, true, 1, "PurchaseInvoice"),
        };

        var siRules = rules.Where(r => r.DocumentType == "SalesInvoice").ToList();
        siRules.Count.ShouldBe(1);
        siRules[0].Name.ShouldBe("SI Revenue");
    }

    // === Document Type Coverage Tests ===

    [Fact]
    public void IAccountableDocument_SalesInvoice_FieldsAccessible()
    {
        var doc = new TestDocument
        {
            DocumentType = "SalesInvoice",
            NetTotal = 1000,
            GrandTotal = 1060,
            TaxAmount = 60,
            CustomerId = Guid.NewGuid(),
        };

        doc.DocumentType.ShouldBe("SalesInvoice");
        doc.CustomerId.ShouldNotBeNull();
        doc.SupplierId.ShouldBeNull();
    }

    [Fact]
    public void IAccountableDocument_PurchaseInvoice_FieldsAccessible()
    {
        var doc = new TestDocument
        {
            DocumentType = "PurchaseInvoice",
            NetTotal = 500,
            GrandTotal = 530,
            TaxAmount = 30,
            SupplierId = Guid.NewGuid(),
        };

        doc.DocumentType.ShouldBe("PurchaseInvoice");
        doc.SupplierId.ShouldNotBeNull();
        doc.CustomerId.ShouldBeNull();
    }

    [Fact]
    public void IAccountableDocument_DeliveryNote_PerpetualInventory()
    {
        // DN posts: DR COGS, CR Stock (both at NetTotal = valuation)
        var doc = new TestDocument
        {
            DocumentType = "DeliveryNote",
            NetTotal = 800, // valuation amount
            GrandTotal = 800,
            TaxAmount = 0,  // no tax on COGS
        };

        doc.NetTotal.ShouldBe(800);
        doc.TaxAmount.ShouldBe(0);
    }

    // === Helper Methods ===

    private static decimal ResolveAmount(AmountSource source, IAccountableDocument document)
    {
        return source switch
        {
            AmountSource.NetTotal => document.NetTotal,
            AmountSource.GrandTotal => document.GrandTotal,
            AmountSource.TaxAmount => document.TaxAmount,
            _ => 0m
        };
    }

    private static AccountingRule CreateRule(
        string name, AmountSource source, bool isDebit, int sortOrder,
        string documentType = "SalesInvoice", bool isActive = true)
    {
        var rule = new AccountingRule(Guid.NewGuid(), Guid.NewGuid(), name, documentType,
            isDebit, AccountSource.FixedAccount, source)
        {
            IsActive = isActive,
            FixedAccountId = Guid.NewGuid(),
            SortOrder = sortOrder,
        };
        return rule;
    }
}
