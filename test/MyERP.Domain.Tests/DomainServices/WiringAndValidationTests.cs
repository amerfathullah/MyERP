using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.DomainServices;

// ═══════════════════════════════════════════════════════════════════
// Domain-Level Validation Tests — SI/PI/PE
// ═══════════════════════════════════════════════════════════════════

public class InvoiceOpeningStockValidationTests
{
    [Fact]
    public void SI_Submit_OpeningWithUpdateStock_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.Today, null);
        si.AddItem(Guid.NewGuid(), "Item A", 1, 100, 0);
        si.IsOpening = true;
        si.UpdateStock = true;

        Should.Throw<BusinessException>(() => si.Submit())
            .Code.ShouldBe(MyERPDomainErrorCodes.OpeningInvoiceCannotUpdateStock);
    }

    [Fact]
    public void SI_Submit_OpeningWithoutUpdateStock_Succeeds()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-002", DateTime.Today, null);
        si.AddItem(Guid.NewGuid(), "Item A", 1, 100, 0);
        si.IsOpening = true;
        si.UpdateStock = false;

        si.Submit();
        si.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void SI_Submit_NonOpeningWithUpdateStock_Succeeds()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-003", DateTime.Today, null);
        si.AddItem(Guid.NewGuid(), "Item A", 1, 100, 0);
        si.UpdateStock = true;

        si.Submit();
        si.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void PI_Submit_OpeningWithUpdateStock_Throws()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.Today, null);
        pi.AddItem(Guid.NewGuid(), "Item A", 1, 100, 0);
        pi.IsOpening = true;
        pi.UpdateStock = true;

        Should.Throw<BusinessException>(() => pi.Submit())
            .Code.ShouldBe(MyERPDomainErrorCodes.OpeningInvoiceCannotUpdateStock);
    }

    [Fact]
    public void PI_Submit_OpeningWithoutUpdateStock_Succeeds()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-002", DateTime.Today, null);
        pi.AddItem(Guid.NewGuid(), "Item A", 1, 100, 0);
        pi.IsOpening = true;
        pi.UpdateStock = false;

        pi.Submit();
        pi.Status.ShouldBe(DocumentStatus.Submitted);
    }
}

public class PaymentEntryDuplicateReferenceTests
{
    [Fact]
    public void PE_Post_NoDuplicateReferences_Succeeds()
    {
        var pe = CreatePE();
        pe.References.Add(CreateRef(Guid.NewGuid(), "SalesInvoice"));
        pe.References.Add(CreateRef(Guid.NewGuid(), "SalesInvoice"));

        pe.Submit();
        pe.Post();
        pe.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void PE_Post_DuplicateReferences_Throws()
    {
        var pe = CreatePE();
        var invoiceId = Guid.NewGuid();
        pe.References.Add(CreateRef(invoiceId, "SalesInvoice"));
        pe.References.Add(CreateRef(invoiceId, "SalesInvoice")); // Duplicate

        pe.Submit();
        Should.Throw<BusinessException>(() => pe.Post())
            .Code.ShouldBe(MyERPDomainErrorCodes.DuplicatePaymentReference);
    }

    [Fact]
    public void PE_Post_SameIdDifferentType_Succeeds()
    {
        var pe = CreatePE();
        var id = Guid.NewGuid();
        pe.References.Add(CreateRef(id, "SalesInvoice"));
        pe.References.Add(CreateRef(id, "SalesOrder")); // Same ID but different type — OK

        pe.Submit();
        pe.Post();
        pe.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void PE_Post_SingleReference_AlwaysSucceeds()
    {
        var pe = CreatePE();
        pe.References.Add(CreateRef(Guid.NewGuid(), "SalesInvoice"));

        pe.Submit();
        pe.Post();
        pe.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void PE_Post_EmptyReferences_Succeeds()
    {
        var pe = CreatePE();
        pe.Submit();
        pe.Post();
        pe.Status.ShouldBe(DocumentStatus.Posted);
    }

    private static PaymentEntry CreatePE() =>
        new(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive, DateTime.Today,
            1000m, Guid.NewGuid(), Guid.NewGuid());

    private static PaymentEntryReference CreateRef(Guid refId, string refType) =>
        new(Guid.NewGuid(), Guid.NewGuid(), refType, refId, 1000m, 500m, 500m);
}

// ═══════════════════════════════════════════════════════════════════
// TaxWithholdingService Integration Tests
// ═══════════════════════════════════════════════════════════════════

public class TaxWithholdingIntegrationTests
{
    [Fact]
    public void OnceDeductedAlwaysDeducted_ForcesCrossing()
    {
        var svc = new TaxWithholdingService(null!);

        // First: below single threshold (10K) so no withholding
        var r1 = svc.CalculateWithholding(5000m, 0m, 10m, 10000m, 0m, false, 0);
        r1.ThresholdCrossed.ShouldBeFalse();

        // With "once deducted" rule: use single threshold of 0 (always exceeded)
        // to simulate the forced calculation when historical withholding exists
        var r2 = svc.CalculateWithholding(5000m, 0m, 10m, 1m, 0m, false, 0);
        r2.ThresholdCrossed.ShouldBeTrue();
        r2.WithheldAmount.ShouldBe(500m); // 5000 × 10%
    }

    [Fact]
    public void DistributeTds_ProportionalAllocation()
    {
        var items = new System.Collections.Generic.List<(Guid, decimal)>
        {
            (Guid.NewGuid(), 600m),
            (Guid.NewGuid(), 400m),
        };

        var dist = TaxWithholdingService.DistributeTdsAcrossItems(100m, items);
        dist.Count.ShouldBe(2);
        dist.Values.Sum().ShouldBe(100m);
        // First item: 600/1000 × 100 = 60
        dist[items[0].Item1].ShouldBe(60m);
        // Second item absorbs remainder
        dist[items[1].Item1].ShouldBe(40m);
    }
}

// ═══════════════════════════════════════════════════════════════════
// Supplier TaxWithholdingCategory Tests
// ═══════════════════════════════════════════════════════════════════

public class SupplierWithholdingTests
{
    [Fact]
    public void Supplier_TaxWithholdingCategory_DefaultNull()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.TaxWithholdingCategory.ShouldBeNull();
    }

    [Fact]
    public void Supplier_TaxWithholdingCategory_CanBeSet()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.TaxWithholdingCategory = "Section 107A";
        supplier.TaxWithholdingCategory.ShouldBe("Section 107A");
    }
}

// ═══════════════════════════════════════════════════════════════════
// Loan Entity Tests
// ═══════════════════════════════════════════════════════════════════

public class LoanAppServiceEntityTests
{
    [Fact]
    public void Loan_DefaultDraft()
    {
        var loan = CreateLoan();
        loan.Status.ShouldBe(LoanStatus.Draft);
        loan.OutstandingBalance.ShouldBe(100_000m);
    }

    [Fact]
    public void Loan_SanctionThenDisburse()
    {
        var loan = CreateLoan();
        loan.Sanction();
        loan.Status.ShouldBe(LoanStatus.Sanctioned);

        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));
        loan.Status.ShouldBe(LoanStatus.Disbursed);
        loan.RepaymentSchedule.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Loan_CancelFromDraft()
    {
        var loan = CreateLoan();
        loan.Cancel();
        loan.Status.ShouldBe(LoanStatus.Cancelled);
    }

    [Fact]
    public void Loan_DisburseGeneratesSchedule()
    {
        var loan = CreateLoan();
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        // 12-month tenure should generate 12 installments
        loan.RepaymentSchedule.Count.ShouldBe(12);
        loan.Emi.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Loan_RecordRepayment_ReducesOutstanding()
    {
        var loan = CreateLoan();
        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        var initialOutstanding = loan.OutstandingBalance;
        loan.RecordRepayment(5000m, 500m);

        loan.TotalPrincipalRepaid.ShouldBe(5000m);
        loan.TotalInterestCharged.ShouldBe(500m);
        loan.OutstandingBalance.ShouldBe(initialOutstanding - 5000m);
    }

    [Fact]
    public void Loan_InvalidAmount_Throws()
    {
        Should.Throw<BusinessException>(() =>
            new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "LOAN-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
                0, 5.5m, 12));
    }

    [Fact]
    public void Loan_InvalidTenure_Throws()
    {
        Should.Throw<BusinessException>(() =>
            new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "LOAN-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
                100_000m, 5.5m, 0));
    }

    private static Loan CreateLoan() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "LOAN-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            100_000m, 5.5m, 12);
}
