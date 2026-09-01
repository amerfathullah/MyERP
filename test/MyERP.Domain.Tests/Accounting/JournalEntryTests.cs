using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class JournalEntryTests
{
    [Fact]
    public void Validate_BalancedEntry_ShouldNotThrow()
    {
        // Arrange: Sales Invoice RM1,000 + SST RM60
        var journal = CreateJournalEntry();
        var receivableAccountId = Guid.NewGuid();
        var revenueAccountId = Guid.NewGuid();
        var taxPayableAccountId = Guid.NewGuid();

        journal.AddLine(receivableAccountId, 1060m, isDebit: true, "Accounts Receivable");
        journal.AddLine(revenueAccountId, 1000m, isDebit: false, "Sales Revenue");
        journal.AddLine(taxPayableAccountId, 60m, isDebit: false, "SST Payable");

        // Act & Assert: should not throw
        journal.Validate();
        journal.TotalDebit.ShouldBe(1060m);
        journal.TotalCredit.ShouldBe(1060m);
    }

    [Fact]
    public void Validate_UnbalancedEntry_ShouldThrow()
    {
        var journal = CreateJournalEntry();
        journal.AddLine(Guid.NewGuid(), 1000m, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 500m, isDebit: false);

        // Act & Assert
        var ex = Assert.Throws<BusinessException>(() => journal.Validate());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.UnbalancedJournalEntry);
    }

    [Fact]
    public void Validate_EmptyEntry_ShouldThrow()
    {
        var journal = CreateJournalEntry();

        Assert.Throws<BusinessException>(() => journal.Validate());
    }

    [Fact]
    public void Post_ValidEntry_ShouldChangeStatus()
    {
        var journal = CreateJournalEntry();
        journal.AddLine(Guid.NewGuid(), 500m, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 500m, isDebit: false);

        journal.Post();

        journal.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void Post_AlreadyPosted_ShouldThrow()
    {
        var journal = CreateJournalEntry();
        journal.AddLine(Guid.NewGuid(), 100m, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 100m, isDebit: false);
        journal.Post();

        Assert.Throws<BusinessException>(() => journal.Post());
    }

    [Fact]
    public void Cancel_PostedEntry_ShouldChangeStatus()
    {
        var journal = CreateJournalEntry();
        journal.AddLine(Guid.NewGuid(), 200m, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 200m, isDebit: false);
        journal.Post();

        journal.Cancel();

        journal.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void AddLine_AfterPost_ShouldThrow()
    {
        var journal = CreateJournalEntry();
        journal.AddLine(Guid.NewGuid(), 100m, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 100m, isDebit: false);
        journal.Post();

        Assert.Throws<BusinessException>(() =>
            journal.AddLine(Guid.NewGuid(), 50m, isDebit: true));
    }

    [Fact]
    public void VoucherType_DefaultsToJournalEntry()
    {
        var journal = CreateJournalEntry();
        journal.VoucherType.ShouldBe(JournalEntryVoucherType.JournalEntry);
    }

    [Theory]
    [InlineData(JournalEntryVoucherType.BankEntry)]
    [InlineData(JournalEntryVoucherType.CashEntry)]
    [InlineData(JournalEntryVoucherType.CreditNote)]
    [InlineData(JournalEntryVoucherType.DebitNote)]
    [InlineData(JournalEntryVoucherType.ContraEntry)]
    [InlineData(JournalEntryVoucherType.WriteOffEntry)]
    [InlineData(JournalEntryVoucherType.OpeningEntry)]
    [InlineData(JournalEntryVoucherType.InterCompanyJournalEntry)]
    public void VoucherType_CanBeSetToAllUserSelectableTypes(JournalEntryVoucherType voucherType)
    {
        var journal = CreateJournalEntry();
        journal.VoucherType = voucherType;
        journal.VoucherType.ShouldBe(voucherType);
    }

    [Fact]
    public void VoucherType_ReversalType_SetByReversalFlow()
    {
        // Reversal type is set programmatically, not by user selection
        var journal = CreateJournalEntry();
        journal.VoucherType = JournalEntryVoucherType.Reversal;
        journal.ReversalOfId = Guid.NewGuid();
        journal.VoucherType.ShouldBe(JournalEntryVoucherType.Reversal);
        journal.ReversalOfId.ShouldNotBeNull();
    }

    [Fact]
    public void AddReversalLine_FlipsDirectionAndPreservesFieldFidelity()
    {
        var source = CreateJournalEntry();
        var receivableAccountId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        source.AddLineWithParty(receivableAccountId, 1060m, isDebit: true, partyId, "Customer", AccountSubType.AccountsReceivable);
        var costCenterId = Guid.NewGuid();
        source.AddLineWithDimensions(Guid.NewGuid(), 1060m, isDebit: false, costCenterId: costCenterId);
        source.Post();

        var reversal = CreateJournalEntry();
        foreach (var line in source.Lines)
            reversal.AddReversalLine(line);
        reversal.Post();

        reversal.TotalDebit.ShouldBe(source.TotalCredit);
        reversal.TotalCredit.ShouldBe(source.TotalDebit);

        var reversedReceivableLine = reversal.Lines.Single(l => l.AccountId == receivableAccountId);
        reversedReceivableLine.IsDebit.ShouldBeFalse(); // flipped from the source's debit
        reversedReceivableLine.PartyId.ShouldBe(partyId);
        reversedReceivableLine.PartyType.ShouldBe("Customer");

        var reversedCostCenterLine = reversal.Lines.Single(l => l.CostCenterId == costCenterId);
        reversedCostCenterLine.IsDebit.ShouldBeTrue(); // flipped from the source's credit
    }

    [Fact]
    public void AddReversalLine_AfterPost_ShouldThrow()
    {
        var source = CreateJournalEntry();
        source.AddLine(Guid.NewGuid(), 100m, isDebit: true);
        source.AddLine(Guid.NewGuid(), 100m, isDebit: false);
        source.Post();

        var reversal = CreateJournalEntry();
        foreach (var line in source.Lines)
            reversal.AddReversalLine(line);
        reversal.Post();

        Assert.Throws<BusinessException>(() => reversal.AddReversalLine(source.Lines[0]));
    }

    [Fact]
    public void AddReconciliationLine_BuildsBalancedLineWithFullFidelity()
    {
        var journal = CreateJournalEntry();
        var receivableAccountId = Guid.NewGuid();
        var payableAccountId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var costCenterId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        journal.AddReconciliationLine(
            receivableAccountId, 500m, isDebit: false,
            customerId, "Customer", costCenterId, projectId: null,
            "MYR", 500m, 1m,
            "SalesInvoice", invoiceId, isAdvance: false,
            description: "Common Party Accounting reconciliation");

        journal.AddReconciliationLine(
            payableAccountId, 500m, isDebit: true,
            supplierId, "Supplier", costCenterId, projectId: null,
            "MYR", 500m, 1m,
            null, null, isAdvance: true,
            description: "Common Party Accounting advance");

        journal.Validate();
        journal.TotalDebit.ShouldBe(500m);
        journal.TotalCredit.ShouldBe(500m);

        var reconciliationLine = journal.Lines.Single(l => l.AccountId == receivableAccountId);
        reconciliationLine.PartyId.ShouldBe(customerId);
        reconciliationLine.PartyType.ShouldBe("Customer");
        reconciliationLine.AgainstVoucherType.ShouldBe("SalesInvoice");
        reconciliationLine.AgainstVoucherId.ShouldBe(invoiceId);
        reconciliationLine.IsAdvance.ShouldBeFalse();

        var advanceLine = journal.Lines.Single(l => l.AccountId == payableAccountId);
        advanceLine.PartyId.ShouldBe(supplierId);
        advanceLine.PartyType.ShouldBe("Supplier");
        advanceLine.AgainstVoucherType.ShouldBeNull();
        advanceLine.IsAdvance.ShouldBeTrue();
    }

    [Fact]
    public void AddReconciliationLine_AfterPost_ShouldThrow()
    {
        var journal = CreateJournalEntry();
        journal.AddLine(Guid.NewGuid(), 100m, isDebit: true);
        journal.AddLine(Guid.NewGuid(), 100m, isDebit: false);
        journal.Post();

        Assert.Throws<BusinessException>(() =>
            journal.AddReconciliationLine(
                Guid.NewGuid(), 100m, isDebit: true,
                Guid.NewGuid(), "Customer", null, null,
                "MYR", 100m, 1m,
                null, null, isAdvance: false));
    }

    private static JournalEntry CreateJournalEntry()
    {
        return new JournalEntry(
            Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            fiscalYearId: Guid.NewGuid(),
            postingDate: DateTime.Today);
    }

    [Fact]
    public void JournalEntry_ReversalProperties_CanBeSet()
    {
        var sourceId = Guid.NewGuid();
        var entry = CreateJournalEntry();
        entry.VoucherType = JournalEntryVoucherType.Reversal;
        entry.ReversalOfId = sourceId;

        entry.VoucherType.ShouldBe(JournalEntryVoucherType.Reversal);
        entry.ReversalOfId.ShouldBe(sourceId);
    }

    [Fact]
    public void ReverseJournalEntry_ReversalOfEntry_IsIdentified()
    {
        // Per ERPNext PR #58092 / commit 9dd37d5f32:
        // An entry with ReversalOfId cannot be reversed again (must cancel instead).
        var source = CreateJournalEntry();
        source.ReversalOfId = Guid.NewGuid();

        source.ReversalOfId.HasValue.ShouldBeTrue();
    }

    [Fact]
    public void PurchaseInvoice_IsBlocked_EvaluatesOnHoldAndReleaseDate()
    {
        var pi = new Purchasing.Entities.PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PINV-2026-0001", DateTime.UtcNow);

        pi.IsBlocked.ShouldBeFalse();

        pi.OnHold = true;
        pi.ReleaseDate = null; // indefinite hold
        pi.IsBlocked.ShouldBeTrue();

        pi.ReleaseDate = DateTime.UtcNow.AddDays(5);
        pi.IsBlockedOnDate(DateTime.UtcNow).ShouldBeTrue();

        pi.ReleaseDate = DateTime.UtcNow.AddDays(-1); // already expired
        pi.IsBlockedOnDate(DateTime.UtcNow).ShouldBeFalse();
    }
}
