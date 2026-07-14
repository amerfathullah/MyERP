using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class BatchPaymentServiceTests
{
    [Fact]
    public void ValidateBatch_EmptyItems_ReturnsError()
    {
        var service = CreateService();
        var input = new BatchPaymentInput
        {
            CompanyId = Guid.NewGuid(),
            PaidFromAccountId = Guid.NewGuid(),
            PaidToAccountId = Guid.NewGuid(),
            Items = new List<BatchPaymentItem>()
        };

        var errors = service.ValidateBatch(input);
        errors.ShouldContain(e => e.Contains("No payment items"));
    }

    [Fact]
    public void ValidateBatch_MissingAccounts_ReturnsErrors()
    {
        var service = CreateService();
        var input = new BatchPaymentInput
        {
            CompanyId = Guid.NewGuid(),
            PaidFromAccountId = Guid.Empty,
            PaidToAccountId = Guid.Empty,
            Items = new List<BatchPaymentItem>
            {
                new() { PartyId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(), Amount = 100 }
            }
        };

        var errors = service.ValidateBatch(input);
        errors.ShouldContain(e => e.Contains("Source account"));
        errors.ShouldContain(e => e.Contains("Destination account"));
    }

    [Fact]
    public void ValidateBatch_NegativeAmount_ReturnsError()
    {
        var service = CreateService();
        var input = CreateValidInput();
        input.Items[0].Amount = -100;

        var errors = service.ValidateBatch(input);
        errors.ShouldContain(e => e.Contains("amount must be positive"));
    }

    [Fact]
    public void ValidateBatch_ZeroAmount_ReturnsError()
    {
        var service = CreateService();
        var input = CreateValidInput();
        input.Items[0].Amount = 0;

        var errors = service.ValidateBatch(input);
        errors.ShouldContain(e => e.Contains("amount must be positive"));
    }

    [Fact]
    public void ValidateBatch_DuplicateInvoice_ReturnsError()
    {
        var service = CreateService();
        var invoiceId = Guid.NewGuid();
        var input = new BatchPaymentInput
        {
            CompanyId = Guid.NewGuid(),
            PaidFromAccountId = Guid.NewGuid(),
            PaidToAccountId = Guid.NewGuid(),
            Items = new List<BatchPaymentItem>
            {
                new() { PartyId = Guid.NewGuid(), InvoiceId = invoiceId, Amount = 100 },
                new() { PartyId = Guid.NewGuid(), InvoiceId = invoiceId, Amount = 200 }
            }
        };

        var errors = service.ValidateBatch(input);
        errors.ShouldContain(e => e.Contains("appears") && e.Contains("times"));
    }

    [Fact]
    public void ValidateBatch_ValidInput_ReturnsNoErrors()
    {
        var service = CreateService();
        var input = CreateValidInput();

        var errors = service.ValidateBatch(input);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateBatch_MultipleItems_ValidatesAll()
    {
        var service = CreateService();
        var input = new BatchPaymentInput
        {
            CompanyId = Guid.NewGuid(),
            PaidFromAccountId = Guid.NewGuid(),
            PaidToAccountId = Guid.NewGuid(),
            Items = new List<BatchPaymentItem>
            {
                new() { PartyId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(), Amount = 500 },
                new() { PartyId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(), Amount = 300 },
                new() { PartyId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(), Amount = 200 }
            }
        };

        var errors = service.ValidateBatch(input);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateBatch_EmptyPartyId_ReturnsError()
    {
        var service = CreateService();
        var input = new BatchPaymentInput
        {
            CompanyId = Guid.NewGuid(),
            PaidFromAccountId = Guid.NewGuid(),
            PaidToAccountId = Guid.NewGuid(),
            Items = new List<BatchPaymentItem>
            {
                new() { PartyId = Guid.Empty, InvoiceId = Guid.NewGuid(), Amount = 100 }
            }
        };

        var errors = service.ValidateBatch(input);
        errors.ShouldContain(e => e.Contains("party is required"));
    }

    #region BatchPaymentResult Tests

    [Fact]
    public void BatchPaymentResult_Default_EmptyState()
    {
        var result = new BatchPaymentResult();
        result.SuccessCount.ShouldBe(0);
        result.ErrorCount.ShouldBe(0);
        result.HasErrors.ShouldBeFalse();
        result.TotalAmount.ShouldBe(0m);
    }

    [Fact]
    public void BatchPaymentResult_WithErrors_HasErrorsTrue()
    {
        var result = new BatchPaymentResult();
        result.Errors.Add(new BatchPaymentError(Guid.NewGuid(), Guid.NewGuid(), "test error"));
        result.HasErrors.ShouldBeTrue();
        result.ErrorCount.ShouldBe(1);
    }

    [Fact]
    public void BatchPaymentInput_GroupByParty_DefaultTrue()
    {
        var input = new BatchPaymentInput();
        input.GroupByParty.ShouldBeTrue();
    }

    [Fact]
    public void BatchPaymentInput_PartyType_DefaultSupplier()
    {
        var input = new BatchPaymentInput();
        input.PartyType.ShouldBe("Supplier");
    }

    [Fact]
    public void BatchPaymentItem_ExchangeRate_DefaultOne()
    {
        var item = new BatchPaymentItem();
        item.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void BatchPaymentError_Record_Properties()
    {
        var partyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var error = new BatchPaymentError(partyId, invoiceId, "insufficient funds");

        error.PartyId.ShouldBe(partyId);
        error.InvoiceId.ShouldBe(invoiceId);
        error.Message.ShouldBe("insufficient funds");
    }

    #endregion

    #region Helpers

    private static BatchPaymentService CreateService()
    {
        // For unit tests, we only test validation logic (no repo needed)
        return new BatchPaymentService(null!, null!);
    }

    private static BatchPaymentInput CreateValidInput()
    {
        return new BatchPaymentInput
        {
            CompanyId = Guid.NewGuid(),
            PaidFromAccountId = Guid.NewGuid(),
            PaidToAccountId = Guid.NewGuid(),
            PaymentType = PaymentType.Pay,
            Items = new List<BatchPaymentItem>
            {
                new()
                {
                    PartyId = Guid.NewGuid(),
                    InvoiceId = Guid.NewGuid(),
                    InvoiceType = "PurchaseInvoice",
                    TotalAmount = 1000,
                    Outstanding = 1000,
                    Amount = 1000
                }
            }
        };
    }

    #endregion
}
