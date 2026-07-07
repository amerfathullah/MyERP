using System;
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

    private static JournalEntry CreateJournalEntry()
    {
        return new JournalEntry(
            Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            fiscalYearId: Guid.NewGuid(),
            postingDate: DateTime.Today);
    }
}
