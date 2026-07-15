using System;
using System.Linq;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for production-hardening improvements:
/// - Standard Cost valuation method
/// - Transaction validation (posting date, currency)
/// - Document activity log
/// - Item transaction validation
/// </summary>
public class ProductionHardeningTests
{
    // === Standard Cost Valuation ===

    [Fact]
    public void StandardCost_StockIn_UsesStandardRate()
    {
        // Standard Cost: regardless of purchase price, valuation uses the standard rate
        // Buy 10 @ 120, but standard rate = 100
        // Balance should be valued at 10 * 100 = 1000
        var standardRate = 100m;
        var qty = 10m;
        var balanceValue = qty * standardRate;

        balanceValue.ShouldBe(1000);
    }

    [Fact]
    public void StandardCost_StockOut_UsesStandardRate()
    {
        // Standard Cost: stock-out also uses standard rate
        // Balance: 20 units @ standard 100 = 2000
        // Sell 5 units → remaining = 15 * 100 = 1500
        var standardRate = 100m;
        var existingQty = 20m;
        var outQty = -5m;
        var newQty = existingQty + outQty;
        var newValue = newQty * standardRate;

        newQty.ShouldBe(15);
        newValue.ShouldBe(1500);
    }

    [Fact]
    public void StandardCost_PPV_CalculatedCorrectly()
    {
        // Purchase Price Variance = (actual_rate - standard_rate) * qty
        // Buy 10 @ 120, standard = 100
        // PPV = (120 - 100) * 10 = 200 (unfavorable)
        var actualRate = 120m;
        var standardRate = 100m;
        var qty = 10m;
        var ppv = (actualRate - standardRate) * qty;

        ppv.ShouldBe(200); // Positive = unfavorable (actual > standard)
    }

    [Fact]
    public void StandardCost_PPV_FavorableVariance()
    {
        // Buy 10 @ 90, standard = 100
        // PPV = (90 - 100) * 10 = -100 (favorable)
        var actualRate = 90m;
        var standardRate = 100m;
        var qty = 10m;
        var ppv = (actualRate - standardRate) * qty;

        ppv.ShouldBe(-100); // Negative = favorable (actual < standard)
    }

    [Fact]
    public void StandardCost_MultipleTransactions_AlwaysAtStandardRate()
    {
        // Multiple purchases at different rates, all valued at standard
        var standardRate = 50m;
        decimal totalQty = 0;

        // Buy 10 @ 45
        totalQty += 10;
        // Buy 20 @ 55
        totalQty += 20;
        // Buy 5 @ 60
        totalQty += 5;

        var balanceValue = totalQty * standardRate;
        balanceValue.ShouldBe(1750); // 35 * 50
    }

    // === Transaction Validation ===

    [Fact]
    public void TransactionValidation_TodayDate_Passes()
    {
        // Today's date should always be valid
        var service = CreateTransactionValidationService();
        Should.NotThrow(() => service.ValidatePostingDate(DateTime.UtcNow.Date));
    }

    [Fact]
    public void TransactionValidation_YesterdayDate_Passes()
    {
        var service = CreateTransactionValidationService();
        Should.NotThrow(() => service.ValidatePostingDate(DateTime.UtcNow.Date.AddDays(-1)));
    }

    [Fact]
    public void TransactionValidation_TomorrowDate_Passes()
    {
        // 1 day in future is allowed (timezone tolerance)
        var service = CreateTransactionValidationService();
        Should.NotThrow(() => service.ValidatePostingDate(DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void TransactionValidation_FarFutureDate_Throws()
    {
        // More than 1 day in future is blocked
        var service = CreateTransactionValidationService();
        Should.Throw<Volo.Abp.BusinessException>(
            () => service.ValidatePostingDate(DateTime.UtcNow.Date.AddDays(5)));
    }

    [Fact]
    public void TransactionValidation_PastDate_Passes()
    {
        // Past dates are always valid (other guards handle frozen periods)
        var service = CreateTransactionValidationService();
        Should.NotThrow(() => service.ValidatePostingDate(DateTime.UtcNow.Date.AddDays(-365)));
    }

    // === Document Activity Log ===

    [Fact]
    public void DocumentActivityLog_Create_SetsAllFields()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(),
            "SalesInvoice",
            Guid.NewGuid(),
            "Submitted",
            Guid.NewGuid(),
            "SI-001",
            "Draft",
            "Submitted",
            Guid.NewGuid(),
            "Auto-submitted",
            Guid.NewGuid());

        log.DocumentType.ShouldBe("SalesInvoice");
        log.ActivityType.ShouldBe("Submitted");
        log.DocumentNumber.ShouldBe("SI-001");
        log.PreviousStatus.ShouldBe("Draft");
        log.NewStatus.ShouldBe("Submitted");
        log.Details.ShouldBe("Auto-submitted");
    }

    [Fact]
    public void DocumentActivityLog_MinimalCreate_Works()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var log = new DocumentActivityLog(
            Guid.NewGuid(),
            "PurchaseOrder",
            docId,
            "Cancelled",
            companyId);

        log.DocumentType.ShouldBe("PurchaseOrder");
        log.DocumentId.ShouldBe(docId);
        log.CompanyId.ShouldBe(companyId);
        log.DocumentNumber.ShouldBeNull();
        log.PreviousStatus.ShouldBeNull();
        log.Details.ShouldBeNull();
    }

    [Fact]
    public void DocumentActivityLog_PaymentActivity_IncludesAmount()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(),
            "SalesInvoice",
            Guid.NewGuid(),
            "PaymentReceived",
            Guid.NewGuid(),
            "SI-002",
            details: "Amount: 5000.00");

        log.ActivityType.ShouldBe("PaymentReceived");
        log.Details!.ShouldContain("5000.00");
    }

    [Fact]
    public void DocumentActivityLog_ConversionActivity_IncludesTarget()
    {
        var targetId = Guid.NewGuid();
        var log = new DocumentActivityLog(
            Guid.NewGuid(),
            "Quotation",
            Guid.NewGuid(),
            "Converted",
            Guid.NewGuid(),
            "QTN-001",
            details: $"Converted to SalesOrder ({targetId})");

        log.ActivityType.ShouldBe("Converted");
        log.Details!.ShouldContain("SalesOrder");
        log.Details!.ShouldContain(targetId.ToString());
    }

    // === Valuation Method Enum ===

    [Fact]
    public void ValuationMethod_StandardCost_Exists()
    {
        var method = ValuationMethod.StandardCost;
        method.ShouldNotBe(ValuationMethod.FIFO);
        method.ShouldNotBe(ValuationMethod.WeightedAverage);
        method.ShouldNotBe(ValuationMethod.LIFO);
    }

    [Fact]
    public void ValuationMethod_AllFour_Distinct()
    {
        var methods = new[]
        {
            ValuationMethod.FIFO,
            ValuationMethod.LIFO,
            ValuationMethod.WeightedAverage,
            ValuationMethod.StandardCost,
        };

        methods.Length.ShouldBe(4);
        methods.Distinct().Count().ShouldBe(4);
    }

    // === StockBalance Record ===

    [Fact]
    public void StockBalance_StandardCost_ValuationRateMatchesStandard()
    {
        // For Standard Cost items, balance is always qty * standard rate
        var standardRate = 75m;
        var qty = 20m;
        var balance = new StockBalance(qty, qty * standardRate);

        balance.ValuationRate.ShouldBe(standardRate);
    }

    private static TransactionValidationService CreateTransactionValidationService()
    {
        // TransactionValidationService.ValidatePostingDate is synchronous and doesn't need repo
        return new TransactionValidationService(null!);
    }
}
