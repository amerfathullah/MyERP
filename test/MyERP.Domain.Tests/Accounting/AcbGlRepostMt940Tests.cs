using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Tests for ACB AppService DTOs, GL Repost service, and MT940 parser.
/// </summary>
public class AcbGlRepostMt940Tests
{
    // ===== AccountClosingBalance Entity Tests =====

    [Fact]
    public void AccountClosingBalance_Defaults()
    {
        var acb = new AccountClosingBalance(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30), "2026-06", 10000m, 8000m);

        Assert.Equal(10000m, acb.Debit);
        Assert.Equal(8000m, acb.Credit);
        Assert.Equal(2000m, acb.Balance); // Debit - Credit
        Assert.Equal("2026-06", acb.Period);
    }

    [Fact]
    public void AccountClosingBalance_NegativeBalance_WhenCreditExceedsDebit()
    {
        var acb = new AccountClosingBalance(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30), "2026-06", 5000m, 12000m);

        Assert.Equal(-7000m, acb.Balance);
    }

    [Fact]
    public void AccountClosingBalance_ZeroBalance()
    {
        var acb = new AccountClosingBalance(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30), "2026-06", 0m, 0m);

        Assert.Equal(0m, acb.Balance);
    }

    [Fact]
    public void AccountClosingBalance_WithCostCenter()
    {
        var ccId = Guid.NewGuid();
        var acb = new AccountClosingBalance(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30), "2026-06", 1000m, 500m,
            costCenterId: ccId);

        Assert.Equal(ccId, acb.CostCenterId);
    }

    [Fact]
    public void AccountClosingBalance_WithFinanceBook()
    {
        var acb = new AccountClosingBalance(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30), "2026-06", 1000m, 500m,
            financeBook: "Tax Book");

        Assert.Equal("Tax Book", acb.FinanceBook);
    }

    // ===== ClosingBalanceStatusDto Tests =====

    [Fact]
    public void ClosingBalanceStatusDto_DefaultsToEmpty()
    {
        var dto = new ClosingBalanceStatusDto();
        Assert.Null(dto.LatestPeriod);
        Assert.Null(dto.LatestClosingDate);
        Assert.Equal(0, dto.TotalBalances);
        Assert.False(dto.IsBalanced);
    }

    [Fact]
    public void ClosingBalanceStatusDto_IsBalanced_WhenDebitEqualsCredit()
    {
        var dto = new ClosingBalanceStatusDto
        {
            TotalDebit = 50000m,
            TotalCredit = 50000m,
            IsBalanced = Math.Abs(50000m - 50000m) < 0.01m
        };
        Assert.True(dto.IsBalanced);
    }

    // ===== GlRepostService Static Tests =====

    [Theory]
    [InlineData("SalesInvoice", true)]
    [InlineData("PurchaseInvoice", true)]
    [InlineData("PaymentEntry", true)]
    [InlineData("JournalEntry", true)]
    [InlineData("PurchaseReceipt", true)]
    [InlineData("DeliveryNote", true)]
    [InlineData("StockEntry", true)]
    [InlineData("MaterialRequest", false)]
    [InlineData("Quotation", false)]
    [InlineData("SalesOrder", false)]
    [InlineData("Unknown", false)]
    public void GlRepostService_IsRepostAllowed(string voucherType, bool expected)
    {
        Assert.Equal(expected, GlRepostService.IsRepostAllowed(voucherType));
    }

    [Fact]
    public void GlRepostService_AllowedTypes_Contains7Types()
    {
        Assert.Equal(7, GlRepostService.AllowedVoucherTypes.Count);
    }

    [Fact]
    public void GlRepostResult_Properties()
    {
        var result = new GlRepostResult(5, 2, 1, ["Error1"]);
        Assert.Equal(5, result.SuccessCount);
        Assert.Equal(2, result.SkippedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(8, result.TotalProcessed);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void GlRepostResult_NoErrors()
    {
        var result = new GlRepostResult(10, 0, 0, []);
        Assert.False(result.HasErrors);
        Assert.Equal(10, result.TotalProcessed);
    }

    // ===== GlRepostAppService DTO Tests =====

    [Fact]
    public void GlRepostResultDto_Defaults()
    {
        var dto = new GlRepostResultDto();
        Assert.Equal(0, dto.SuccessCount);
        Assert.Equal(0, dto.TotalProcessed);
        Assert.False(dto.HasErrors);
        Assert.Empty(dto.Errors);
    }

    [Fact]
    public void RepostBatchGlDto_CanAddVouchers()
    {
        var dto = new RepostBatchGlDto
        {
            CompanyId = Guid.NewGuid(),
            Vouchers = [
                new RepostVoucherRefDto { VoucherType = "SalesInvoice", VoucherId = Guid.NewGuid() },
                new RepostVoucherRefDto { VoucherType = "PurchaseReceipt", VoucherId = Guid.NewGuid() }
            ]
        };
        Assert.Equal(2, dto.Vouchers.Count);
    }

    // ===== MT940 Parser Tests =====

    [Fact]
    public void Mt940Parser_EmptyContent_ReturnsError()
    {
        var parser = new Mt940Parser();
        var result = parser.Parse("");
        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.TransactionCount);
    }

    [Fact]
    public void Mt940Parser_MinimalStatement_ParsesMetadata()
    {
        var mt940 = @":20:REFERENCE1
:25:DE1234567890
:28C:12345/1
:60F:C260630MYR50000,00
:62F:C260630MYR51500,00";

        var parser = new Mt940Parser();
        var result = parser.Parse(mt940);

        Assert.Equal("DE1234567890", result.AccountIdentification);
        Assert.Equal("REFERENCE1", result.StatementReference);
        Assert.Equal("MYR", result.Currency);
    }

    [Fact]
    public void Mt940Parser_CreditTransaction()
    {
        var mt940 = @":20:TEST
:25:ACCT001
:60F:C260101MYR10000,00
:61:260101C5000,00NTRFCUSTREF123456//BANKREF789
:86:Customer payment
:62F:C260101MYR15000,00";

        var parser = new Mt940Parser();
        var result = parser.Parse(mt940);

        Assert.True(result.TransactionCount >= 1);
        var tx = result.Transactions[0];
        Assert.True(tx.IsCredit);
        Assert.Equal(5000m, tx.Amount);
        Assert.Equal(5000m, tx.SignedAmount);
    }

    [Fact]
    public void Mt940Parser_DebitTransaction()
    {
        var mt940 = @":20:TEST
:25:ACCT001
:60F:C260101MYR10000,00
:61:260101D2500,00NTRFSUPPLIER001//BANKREF002
:86:Supplier payment
:62F:C260101MYR7500,00";

        var parser = new Mt940Parser();
        var result = parser.Parse(mt940);

        Assert.True(result.TransactionCount >= 1);
        var tx = result.Transactions[0];
        Assert.False(tx.IsCredit);
        Assert.Equal(2500m, tx.Amount);
        Assert.Equal(-2500m, tx.SignedAmount);
    }

    [Fact]
    public void Mt940_ExtractReference_NormalCustomerRef()
    {
        var tx = new Mt940Transaction { CustomerReference = "INV-2026-001" };
        Assert.Equal("INV-2026-001", Mt940Parser.ExtractReference(tx));
    }

    [Fact]
    public void Mt940_ExtractReference_NONREF_FallsToBankRef()
    {
        var tx = new Mt940Transaction { CustomerReference = "NONREF", BankReference = "BANK123" };
        Assert.Equal("BANK123", Mt940Parser.ExtractReference(tx));
    }

    [Fact]
    public void Mt940_ExtractReference_NONREF_CaseInsensitive()
    {
        var tx = new Mt940Transaction { CustomerReference = "nonref", BankReference = "REF456" };
        Assert.Equal("REF456", Mt940Parser.ExtractReference(tx));
    }

    [Fact]
    public void Mt940_ExtractReference_16CharOverflow_ConcatenatesExtra()
    {
        var tx = new Mt940Transaction
        {
            CustomerReference = "1234567890123456",  // Exactly 16 chars
            ExtraDetails = "789EXTRA"
        };
        Assert.Equal("1234567890123456789EXTRA", Mt940Parser.ExtractReference(tx));
    }

    [Fact]
    public void Mt940_ExtractReference_Under16Chars_NoExtraConcat()
    {
        var tx = new Mt940Transaction
        {
            CustomerReference = "SHORT123",  // Under 16 chars
            ExtraDetails = "EXTRA"
        };
        Assert.Equal("SHORT123", Mt940Parser.ExtractReference(tx)); // No concatenation
    }

    [Fact]
    public void Mt940_ExtractReference_AllNull_ReturnsNull()
    {
        var tx = new Mt940Transaction();
        Assert.Null(Mt940Parser.ExtractReference(tx));
    }

    [Fact]
    public void Mt940_ExtractReference_NONREF_AllFallbacksNull_ReturnsNull()
    {
        var tx = new Mt940Transaction { CustomerReference = "NONREF" };
        Assert.Null(Mt940Parser.ExtractReference(tx));
    }

    [Fact]
    public void Mt940_ExtractReference_FallsToTransactionRef()
    {
        var tx = new Mt940Transaction { TransactionReference = "TXN-999" };
        Assert.Equal("TXN-999", Mt940Parser.ExtractReference(tx));
    }

    [Fact]
    public void Mt940ParseResult_Defaults()
    {
        var result = new Mt940ParseResult([], [], null);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.TransactionCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void Mt940ParseResult_WithError_NotSuccess()
    {
        var result = new Mt940ParseResult([], ["Error1"], null);
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public void Mt940Transaction_SignedAmount_CreditPositive()
    {
        var tx = new Mt940Transaction { IsCredit = true, Amount = 100m };
        Assert.Equal(100m, tx.SignedAmount);
    }

    [Fact]
    public void Mt940Transaction_SignedAmount_DebitNegative()
    {
        var tx = new Mt940Transaction { IsCredit = false, Amount = 100m };
        Assert.Equal(-100m, tx.SignedAmount);
    }

    // ===== MT940 Import DTO Tests =====

    [Fact]
    public void Mt940ImportInput_Properties()
    {
        var input = new Mt940ImportInput
        {
            CompanyId = Guid.NewGuid(),
            BankAccountId = Guid.NewGuid(),
            Mt940Content = ":20:TEST",
            CurrencyCode = "MYR"
        };
        Assert.NotNull(input.Mt940Content);
        Assert.Equal("MYR", input.CurrencyCode);
    }

    [Fact]
    public void Mt940Parser_MultipleTransactions()
    {
        var mt940 = @":20:STMT001
:25:MYBANK123
:60F:C260101MYR10000,00
:61:260101C1500,00NTRFCUST001//BANK001
:86:Payment from ABC Trading
:61:260102D500,00NTRFSUPP001//BANK002
:86:Payment to XYZ Supplies
:61:260103C2000,00NTRFCUST002//BANK003
:86:Payment from DEF Corp
:62F:C260103MYR13000,00";

        var parser = new Mt940Parser();
        var result = parser.Parse(mt940);

        Assert.True(result.TransactionCount >= 3);
        Assert.True(result.IsSuccess);
        Assert.Equal("MYBANK123", result.AccountIdentification);
    }

    // ===== AccountClosingBalanceAppService DTO Tests =====

    [Fact]
    public void AccountClosingBalanceDto_Properties()
    {
        var dto = new AccountClosingBalanceDto
        {
            AccountName = "Accounts Receivable",
            AccountCode = "1130",
            Period = "2026-06",
            Debit = 50000m,
            Credit = 35000m,
            Balance = 15000m
        };

        Assert.Equal("1130", dto.AccountCode);
        Assert.Equal(15000m, dto.Balance);
    }

    [Fact]
    public void RebuildClosingBalanceDto_Properties()
    {
        var dto = new RebuildClosingBalanceDto
        {
            CompanyId = Guid.NewGuid(),
            ClosingDate = new DateTime(2026, 6, 30),
            Period = "2026-06"
        };

        Assert.Equal("2026-06", dto.Period);
    }
}
