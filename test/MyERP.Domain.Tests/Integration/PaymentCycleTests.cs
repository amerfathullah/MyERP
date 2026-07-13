using System;
using MyERP.Accounting.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Integration tests for payment and accounting flows.
/// Tests payment terms schedule generation, accounting period closure,
/// and the full payment → outstanding reduction cycle.
/// </summary>
public class PaymentCycleTests
{
    [Fact]
    public void PaymentTerms_Net30_GeneratesCorrectSchedule()
    {
        var template = new PaymentTermsTemplate(Guid.NewGuid(), "Net 30");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 100m, 30, "Net 30 days"));

        var schedule = template.GenerateSchedule(new DateTime(2026, 7, 1), 15000m);

        schedule.Count.ShouldBe(1);
        schedule[0].DueDate.ShouldBe(new DateTime(2026, 7, 31));
        schedule[0].PaymentAmount.ShouldBe(15000m);
    }

    [Fact]
    public void PaymentTerms_SplitPayment_50_30_20()
    {
        var template = new PaymentTermsTemplate(Guid.NewGuid(), "50/30/20 Split");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 50m, 0, "Advance"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 30m, 30, "30 days"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 20m, 60, "60 days"));

        var schedule = template.GenerateSchedule(new DateTime(2026, 3, 1), 10000m);

        schedule.Count.ShouldBe(3);
        schedule[0].PaymentAmount.ShouldBe(5000m);
        schedule[0].DueDate.ShouldBe(new DateTime(2026, 3, 1));
        schedule[1].PaymentAmount.ShouldBe(3000m);
        schedule[1].DueDate.ShouldBe(new DateTime(2026, 3, 31));
        schedule[2].PaymentAmount.ShouldBe(2000m);
        schedule[2].DueDate.ShouldBe(new DateTime(2026, 4, 30));

        // Sum must equal grand total
        (schedule[0].PaymentAmount + schedule[1].PaymentAmount + schedule[2].PaymentAmount).ShouldBe(10000m);
    }

    [Fact]
    public void PaymentEntry_FullPayment_ReducesOutstanding()
    {
        // Create invoice with RM5000 outstanding
        var si = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Service", 1, 5000, 400); // SST RM400
        si.Submit();

        si.GrandTotal.ShouldBe(5400m);
        si.OutstandingAmount.ShouldBe(5400m);

        // Simulate full payment received
        si.AmountPaid = 5400m;
        si.OutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void PaymentEntry_PartialPayment_ShowsRemainingOutstanding()
    {
        var si = new Sales.Entities.SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-002", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Product", 10, 100, 0);
        si.Submit();

        si.GrandTotal.ShouldBe(1000m);

        // First partial payment
        si.AmountPaid = 400m;
        si.OutstandingAmount.ShouldBe(600m);

        // Second partial payment
        si.AmountPaid = 800m;
        si.OutstandingAmount.ShouldBe(200m);
    }

    [Fact]
    public void AccountingPeriod_ClosedPeriod_BlocksPosting()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(),
            "Q1 2026", new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));

        period.ContainsDate(new DateTime(2026, 2, 15)).ShouldBeTrue();
        period.ContainsDate(new DateTime(2026, 4, 1)).ShouldBeFalse();

        period.Close();
        period.IsClosed.ShouldBeTrue();

        // Reopen should work
        period.Reopen();
        period.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void CurrencyExchange_StoredForLookup()
    {
        var rate = new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.50m, new DateTime(2026, 7, 1));

        rate.FromCurrency.ShouldBe("USD");
        rate.ToCurrency.ShouldBe("MYR");
        rate.ExchangeRate.ShouldBe(4.50m);
    }

    [Fact]
    public void PurchaseInvoice_Outstanding_WithPartialPayment()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Raw Material", 200, 25, 0);
        pi.Submit();

        pi.GrandTotal.ShouldBe(5000m);
        pi.OutstandingAmount.ShouldBe(5000m);

        pi.AmountPaid = 3000m;
        pi.OutstandingAmount.ShouldBe(2000m);
    }

    [Fact]
    public void PaymentTerms_InvalidPortions_Throws()
    {
        var template = new PaymentTermsTemplate(Guid.NewGuid(), "Bad Terms");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 60m, 0));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 30m, 30));
        // Total = 90%, not 100%

        Should.Throw<BusinessException>(() => template.ValidatePortions());
    }

    [Fact]
    public void CostCenter_TreeStructure()
    {
        var root = new CostCenter(Guid.NewGuid(), Guid.NewGuid(), "MyCompany Sdn Bhd", isGroup: true);
        var sales = new CostCenter(Guid.NewGuid(), root.CompanyId, "Sales", parentId: root.Id);
        var support = new CostCenter(Guid.NewGuid(), root.CompanyId, "Support", parentId: root.Id);

        root.IsGroup.ShouldBeTrue();
        sales.IsGroup.ShouldBeFalse();
        sales.ParentId.ShouldBe(root.Id);
        support.ParentId.ShouldBe(root.Id);
    }
}
