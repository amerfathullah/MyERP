using System;
using MyERP.Assets.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.DomainServicesRound2;

public class DomainServicesRound2Tests
{
    // ========== AssetRepair Entity Tests ==========

    [Fact]
    public void AssetRepair_Create_SetsDefaults()
    {
        var repair = new AssetRepair(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repair.Status.ShouldBe(AssetRepairStatus.Pending);
        repair.RepairCost.ShouldBe(0);
        repair.CapitalizeRepairCost.ShouldBeFalse();
        repair.IncreaseInAssetLife.ShouldBe(0);
    }

    [Fact]
    public void AssetRepair_Complete_SetsStatus()
    {
        var repair = new AssetRepair(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repair.Complete();
        repair.Status.ShouldBe(AssetRepairStatus.Completed);
        repair.CompletionDate.ShouldNotBeNull();
    }

    [Fact]
    public void AssetRepair_Complete_DoubleComplete_Throws()
    {
        var repair = new AssetRepair(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repair.Complete();
        Should.Throw<BusinessException>(() => repair.Complete());
    }

    [Fact]
    public void AssetRepair_Cancel_FromPending()
    {
        var repair = new AssetRepair(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        repair.Cancel();
        repair.Status.ShouldBe(AssetRepairStatus.Cancelled);
    }

    [Fact]
    public void AssetRepair_FullyDepreciated_ForcesNoCapitalize()
    {
        var repair = new AssetRepair(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            CapitalizeRepairCost = true,
            IncreaseInAssetLife = 12
        };

        repair.ApplyFullyDepreciatedRules(isFullyDepreciated: true);

        repair.CapitalizeRepairCost.ShouldBeFalse();
        repair.IncreaseInAssetLife.ShouldBe(0);
    }

    [Fact]
    public void AssetRepair_NotFullyDepreciated_KeepsCapitalize()
    {
        var repair = new AssetRepair(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            CapitalizeRepairCost = true,
            IncreaseInAssetLife = 12
        };

        repair.ApplyFullyDepreciatedRules(isFullyDepreciated: false);

        repair.CapitalizeRepairCost.ShouldBeTrue();
        repair.IncreaseInAssetLife.ShouldBe(12);
    }

    // ========== AssetCapitalization Entity Tests ==========

    [Fact]
    public void AssetCapitalization_Create_SetsDefaults()
    {
        var cap = new AssetCapitalization(Guid.NewGuid(), Guid.NewGuid(), "CAP-001",
            DateTime.UtcNow, Guid.NewGuid());
        cap.Status.ShouldBe(AssetCapitalizationStatus.Draft);
        cap.TotalCapitalizedAmount.ShouldBe(0);
        cap.StockItems.Count.ShouldBe(0);
        cap.ServiceItems.Count.ShouldBe(0);
        cap.ConsumedAssets.Count.ShouldBe(0);
    }

    [Fact]
    public void AssetCapitalization_AddStockItem_IncreasesTotal()
    {
        var cap = new AssetCapitalization(Guid.NewGuid(), Guid.NewGuid(), "CAP-001",
            DateTime.UtcNow, Guid.NewGuid());
        cap.AddStockItem(Guid.NewGuid(), "Widget", 5m, 100m);

        cap.StockItems.Count.ShouldBe(1);
        cap.TotalCapitalizedAmount.ShouldBe(500m);
    }

    [Fact]
    public void AssetCapitalization_MixedItems_CalculatesTotal()
    {
        var cap = new AssetCapitalization(Guid.NewGuid(), Guid.NewGuid(), "CAP-001",
            DateTime.UtcNow, Guid.NewGuid());
        cap.AddStockItem(Guid.NewGuid(), "Widget", 2m, 100m);    // 200
        cap.AddServiceItem(Guid.NewGuid(), "Installation", 300m); // 300
        cap.AddConsumedAsset(Guid.NewGuid(), "Old Machine", 500m); // 500

        cap.TotalCapitalizedAmount.ShouldBe(1000m);
        cap.StockItems.Count.ShouldBe(1);
        cap.ServiceItems.Count.ShouldBe(1);
        cap.ConsumedAssets.Count.ShouldBe(1);
    }

    [Fact]
    public void AssetCapitalization_AddAfterSubmit_Throws()
    {
        var cap = new AssetCapitalization(Guid.NewGuid(), Guid.NewGuid(), "CAP-001",
            DateTime.UtcNow, Guid.NewGuid());
        cap.Submit();

        Should.Throw<BusinessException>(() =>
            cap.AddStockItem(Guid.NewGuid(), "Widget", 1m, 50m));
    }

    [Fact]
    public void AssetCapitalization_Submit_Cancel_Lifecycle()
    {
        var cap = new AssetCapitalization(Guid.NewGuid(), Guid.NewGuid(), "CAP-001",
            DateTime.UtcNow, Guid.NewGuid());
        cap.Submit();
        cap.Status.ShouldBe(AssetCapitalizationStatus.Submitted);

        cap.Cancel();
        cap.Status.ShouldBe(AssetCapitalizationStatus.Cancelled);
    }

    // ========== LoanManager Tests ==========

    [Fact]
    public void LoanManager_CalculatePayrollDeduction_CappedAtOutstanding()
    {
        var mgr = new LoanManager(null!);
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "L-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            10000m, 5.5m, 12);
        loan.Emi = 900m;
        loan.TotalPrincipalRepaid = 9500m; // Only 500 outstanding

        mgr.CalculatePayrollDeduction(loan).ShouldBe(500m);
    }

    [Fact]
    public void LoanManager_CalculatePayrollDeduction_FullEmi()
    {
        var mgr = new LoanManager(null!);
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "L-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            10000m, 5.5m, 12);
        loan.Emi = 900m;
        loan.TotalPrincipalRepaid = 0m;

        mgr.CalculatePayrollDeduction(loan).ShouldBe(900m);
    }

    [Fact]
    public void LoanManager_CalculatePayrollDeduction_ZeroOutstanding()
    {
        var mgr = new LoanManager(null!);
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "L-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            10000m, 5.5m, 12);
        loan.Emi = 900m;
        loan.TotalPrincipalRepaid = 10000m;

        mgr.CalculatePayrollDeduction(loan).ShouldBe(0m);
    }

    [Fact]
    public void LoanManager_SplitRepayment_Diminishing()
    {
        var mgr = new LoanManager(null!);
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "L-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            12000m, 12m, 12); // 12% annual = 1% monthly
        loan.TotalPrincipalRepaid = 0m;

        var (principal, interest) = mgr.SplitRepayment(loan, 1000m);

        interest.ShouldBe(120m); // 12000 × 1% = 120
        principal.ShouldBe(880m); // 1000 - 120
    }

    [Fact]
    public void LoanManager_SplitRepayment_FlatRate()
    {
        var mgr = new LoanManager(null!);
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "L-001", LoanType.TermLoan, InterestCalculationMethod.FlatRate,
            12000m, 12m, 12); // 12% annual flat
        loan.TotalPrincipalRepaid = 0m;

        var (principal, interest) = mgr.SplitRepayment(loan, 1120m);

        // Flat total interest = 12000 × 12% × (12/12) = 1440; per month = 120
        interest.ShouldBe(120m);
        principal.ShouldBe(1000m);
    }

    [Fact]
    public void LoanManager_CalculatePenalty_Calculates()
    {
        var mgr = new LoanManager(null!);
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "L-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            10000m, 5.5m, 12);
        loan.PenaltyRate = 2m; // 2% annual penalty
        loan.TotalPrincipalRepaid = 0m;

        var penalty = mgr.CalculatePenalty(loan, 30);
        // 10000 × 2% × 30/365 = 16.44
        penalty.ShouldBeGreaterThan(0);
        penalty.ShouldBe(Math.Round(10000m * 0.02m * 30m / 365m, 2));
    }

    [Fact]
    public void LoanManager_CalculatePenalty_ZeroRate()
    {
        var mgr = new LoanManager(null!);
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "L-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance,
            10000m, 5.5m, 12);
        loan.PenaltyRate = 0;

        mgr.CalculatePenalty(loan, 30).ShouldBe(0);
    }

    // ========== ExpenseClaimManager Tests ==========

    [Fact]
    public void ExpenseClaimManager_CalculateReimbursable_Basic()
    {
        var mgr = new ExpenseClaimManager(null!);
        var claim = new ExpenseClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.AddExpense(DateTime.UtcNow, "Travel", 1000m);
        claim.Approve();
        // TotalSanctioned = 1000, Advance = 0, Reimbursed = 0

        mgr.CalculateReimbursableAmount(claim).ShouldBe(1000m);
    }

    [Fact]
    public void ExpenseClaimManager_CalculateReimbursable_WithAdvance()
    {
        var mgr = new ExpenseClaimManager(null!);
        var claim = new ExpenseClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.AddExpense(DateTime.UtcNow, "Travel", 1000m);
        claim.AdvanceAmount = 300m;
        claim.Approve();

        mgr.CalculateReimbursableAmount(claim).ShouldBe(700m);
    }

    [Fact]
    public void ExpenseClaimManager_CalculateReimbursable_FullyReimbursed()
    {
        var mgr = new ExpenseClaimManager(null!);
        var claim = new ExpenseClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.AddExpense(DateTime.UtcNow, "Travel", 1000m);
        claim.Approve();
        claim.TotalAmountReimbursed = 1000m;

        mgr.CalculateReimbursableAmount(claim).ShouldBe(0m);
    }

    [Fact]
    public void ExpenseClaimManager_ValidateForReimbursement_NotSubmitted_Throws()
    {
        var mgr = new ExpenseClaimManager(null!);
        var claim = new ExpenseClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.AddExpense(DateTime.UtcNow, "Travel", 1000m);
        // Still Draft

        Should.Throw<BusinessException>(() => mgr.ValidateForReimbursement(claim));
    }

    [Fact]
    public void ExpenseClaimManager_ValidateAdvanceLinkage_ExceedsPayment_Throws()
    {
        var mgr = new ExpenseClaimManager(null!);
        var claim = new ExpenseClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.AdvancePaymentEntryId = Guid.NewGuid();
        claim.AdvanceAmount = 5000m;

        Should.Throw<BusinessException>(() =>
            mgr.ValidateAdvanceLinkage(claim, advancePaymentAmount: 3000m));
    }

    [Fact]
    public void ExpenseClaimManager_ValidateAdvanceLinkage_WithinLimit_Passes()
    {
        var mgr = new ExpenseClaimManager(null!);
        var claim = new ExpenseClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.AdvancePaymentEntryId = Guid.NewGuid();
        claim.AdvanceAmount = 2000m;

        Should.NotThrow(() =>
            mgr.ValidateAdvanceLinkage(claim, advancePaymentAmount: 3000m));
    }

    // ========== PutawayRule Tests ==========

    [Fact]
    public void PutawayAllocation_HasCorrectFields()
    {
        var alloc = new PutawayAllocation
        {
            WarehouseId = Guid.NewGuid(),
            Qty = 50m,
            PutawayRuleId = Guid.NewGuid(),
            IsUnallocated = false
        };
        alloc.Qty.ShouldBe(50m);
        alloc.IsUnallocated.ShouldBeFalse();
    }

    // ========== PickListManager Tests ==========

    [Fact]
    public void PickListManager_HasPendingTransfers_EmptyList()
    {
        var mgr = new PickListManager(null!, null!);
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        mgr.HasPendingTransfers(pl).ShouldBeFalse();
    }

    [Fact]
    public void PickListManager_GetPendingTransfers_ReturnsOnlyPending()
    {
        var mgr = new PickListManager(null!, null!);
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m, 100m, "Item A");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m, 50m, "Item B");

        var pending = mgr.GetPendingTransfers(pl);
        pending.Count.ShouldBe(2);
        pending[0].PendingQty.ShouldBe(100m);
    }

    [Fact]
    public void PickAllocationResult_HasCorrectDefaults()
    {
        var result = new PickAllocationResult();
        result.HasShortage.ShouldBeFalse();
        result.Allocations.Count.ShouldBe(0);
    }

    // ========== StockReservationManager Tests ==========

    [Fact]
    public void ReservationConsumption_StoresValues()
    {
        var c = new ReservationConsumption
        {
            StockReservationEntryId = Guid.NewGuid(),
            ConsumedQty = 25m
        };
        c.ConsumedQty.ShouldBe(25m);
    }

    [Fact]
    public void PendingTransfer_StoresValues()
    {
        var pt = new PendingTransfer
        {
            PickListItemId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            PendingQty = 100m,
            BatchId = Guid.NewGuid()
        };
        pt.PendingQty.ShouldBe(100m);
        pt.BatchId.ShouldNotBeNull();
    }
}
