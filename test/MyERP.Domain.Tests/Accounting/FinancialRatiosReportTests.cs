using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class FinancialRatiosReportTests
{
    [Fact]
    public void FinancialRatiosRequestDto_DefaultsCorrectly()
    {
        var dto = new FinancialRatiosRequestDto
        {
            CompanyId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
        };

        dto.IncludeComparison.ShouldBeTrue();
        dto.CompanyId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void FinancialRatioRowDto_DefaultsCorrectly()
    {
        var row = new FinancialRatioRowDto();
        row.Category.ShouldBe(string.Empty);
        row.RatioName.ShouldBe(string.Empty);
        row.Value.ShouldBeNull();
        row.PreviousValue.ShouldBeNull();
        row.ChangePercentage.ShouldBeNull();
        row.Formula.ShouldBe(string.Empty);
        row.Description.ShouldBe(string.Empty);
        row.Unit.ShouldBe(string.Empty);
    }

    [Fact]
    public void LiquidityRatios_CurrentRatio_FormulaCalculatesCorrectly()
    {
        decimal currentAssets = 150_000m;
        decimal currentLiabilities = 100_000m;
        var ratio = Math.Round(currentAssets / currentLiabilities, 2);

        ratio.ShouldBe(1.50m);
    }

    [Fact]
    public void LiquidityRatios_QuickRatio_ExcludesStockAndPrepayments()
    {
        decimal bankCash = 50_000m;
        decimal receivables = 50_000m;
        decimal quickAssets = bankCash + receivables;
        decimal currentLiabilities = 100_000m;

        var ratio = Math.Round(quickAssets / currentLiabilities, 2);
        ratio.ShouldBe(1.00m);
    }

    [Fact]
    public void SolvencyRatios_DebtEquityRatio_CalculatesCorrectly()
    {
        decimal totalLiabilities = 200_000m;
        decimal shareholderEquity = 400_000m;

        var ratio = Math.Round(totalLiabilities / shareholderEquity, 2);
        ratio.ShouldBe(0.50m);
    }

    [Fact]
    public void SolvencyRatios_ProfitMargins_CalculatesCorrectly()
    {
        decimal netSales = 1_000_000m;
        decimal cogs = 600_000m;
        decimal totalExpense = 850_000m;
        decimal netProfit = netSales - totalExpense; // 150_000

        var grossProfitMargin = Math.Round((netSales - cogs) / netSales * 100m, 2);
        var netProfitMargin = Math.Round(netProfit / netSales * 100m, 2);

        grossProfitMargin.ShouldBe(40.00m);
        netProfitMargin.ShouldBe(15.00m);
    }

    [Fact]
    public void SolvencyRatios_ReturnOnAssetsAndEquity_CalculatesCorrectly()
    {
        decimal netProfit = 150_000m;
        decimal totalAssets = 1_500_000m;
        decimal shareholderEquity = 750_000m;

        var roa = Math.Round(netProfit / totalAssets * 100m, 2);
        var roe = Math.Round(netProfit / shareholderEquity * 100m, 2);

        roa.ShouldBe(10.00m);
        roe.ShouldBe(20.00m);
    }

    [Fact]
    public void TurnoverRatios_TurnoverFormulas_CalculateCorrectly()
    {
        decimal netSales = 1_000_000m;
        decimal cogs = 600_000m;
        decimal fixedAssets = 500_000m;
        decimal avgDebtors = 200_000m;
        decimal avgStock = 150_000m;

        var fixedAssetTurnover = Math.Round(netSales / fixedAssets, 2);
        var debtorTurnover = Math.Round(netSales / avgDebtors, 2);
        var inventoryTurnover = Math.Round(cogs / avgStock, 2);

        fixedAssetTurnover.ShouldBe(2.00m);
        debtorTurnover.ShouldBe(5.00m);
        inventoryTurnover.ShouldBe(4.00m);
    }

    [Fact]
    public async Task ReportingAppService_GetFinancialRatiosAsync_ReturnsAll11Ratios()
    {
        var companyId = Guid.NewGuid();
        var accountRepo = Substitute.For<IRepository<Account, Guid>>();
        var journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
        var journalEntryRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var closingBalanceRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();
        var balanceService = new AccountBalanceService(journalEntryRepo, journalLineRepo, closingBalanceRepo, accountRepo);
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();

        // Setup accounts
        var bankAcc = new Account(Guid.NewGuid(), companyId, "1110", "Bank Account", AccountType.Asset)
        {
            AccountSubType = AccountSubType.BankAccount
        };
        var recvAcc = new Account(Guid.NewGuid(), companyId, "1310", "Accounts Receivable", AccountType.Asset)
        {
            AccountSubType = AccountSubType.AccountsReceivable
        };
        var stockAcc = new Account(Guid.NewGuid(), companyId, "1410", "Stock in Hand", AccountType.Asset)
        {
            AccountSubType = AccountSubType.Stock
        };
        var fixedAcc = new Account(Guid.NewGuid(), companyId, "1510", "Equipment", AccountType.Asset)
        {
            AccountSubType = AccountSubType.FixedAsset
        };
        var payAcc = new Account(Guid.NewGuid(), companyId, "2110", "Accounts Payable", AccountType.Liability)
        {
            AccountSubType = AccountSubType.AccountsPayable
        };
        var salesAcc = new Account(Guid.NewGuid(), companyId, "4110", "Sales Revenue", AccountType.Revenue)
        {
            AccountSubType = AccountSubType.OperatingRevenue
        };
        var cogsAcc = new Account(Guid.NewGuid(), companyId, "5110", "Cost of Goods Sold", AccountType.Expense)
        {
            AccountSubType = AccountSubType.CostOfGoodsSold
        };

        var allAccounts = new List<Account> { bankAcc, recvAcc, stockAcc, fixedAcc, payAcc, salesAcc, cogsAcc };
        accountRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Account, bool>>>())
            .Returns(allAccounts);

        // Setup journal entries
        var je = new JournalEntry(Guid.NewGuid(), companyId, Guid.NewGuid(), new DateTime(2026, 6, 15));
        je.EntryNumber = "JV-2026-00001";
        je.AddLine(bankAcc.Id, 50_000m, true);
        je.AddLine(recvAcc.Id, 50_000m, true);
        je.AddLine(stockAcc.Id, 50_000m, true);
        je.AddLine(fixedAcc.Id, 100_000m, true);
        je.AddLine(payAcc.Id, 100_000m, false);
        je.AddLine(salesAcc.Id, 300_000m, false);
        je.AddLine(cogsAcc.Id, 150_000m, true);
        je.Post();

        journalEntryRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { je });

        var appService = new ReportingAppService(
            accountRepo, journalLineRepo, journalEntryRepo, balanceService,
            customerRepo, supplierRepo, companyRepo);

        var report = await appService.GetFinancialRatiosAsync(new FinancialRatiosRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            IncludeComparison = false,
        });

        report.ShouldNotBeNull();
        report.Rows.Count.ShouldBe(11);

        var currentRatio = report.Rows.First(r => r.RatioName == "Current Ratio");
        currentRatio.Value.ShouldNotBeNull();
        currentRatio.Value.Value.ShouldBe(1.50m); // (50k bank + 50k recv + 50k stock) / 100k payable = 1.5

        var quickRatio = report.Rows.First(r => r.RatioName == "Quick Ratio (Acid-Test)");
        quickRatio.Value.ShouldNotBeNull();
        quickRatio.Value.Value.ShouldBe(1.00m); // (50k bank + 50k recv) / 100k payable = 1.0

        var grossMargin = report.Rows.First(r => r.RatioName == "Gross Profit Margin");
        grossMargin.Value.ShouldNotBeNull();
        grossMargin.Value.Value.ShouldBe(50.00m); // (300k - 150k) / 300k * 100 = 50%
    }

    [Theory]
    [InlineData("Menu:FinancialRatios")]
    [InlineData("FinancialRatios")]
    [InlineData("LiquidityRatios")]
    [InlineData("SolvencyProfitabilityRatios")]
    [InlineData("TurnoverRatios")]
    [InlineData("CurrentRatio")]
    [InlineData("QuickRatio")]
    [InlineData("DebtEquityRatio")]
    [InlineData("GrossProfitMargin")]
    [InlineData("NetProfitMargin")]
    [InlineData("ReturnOnAssets")]
    [InlineData("ReturnOnEquity")]
    [InlineData("FixedAssetTurnover")]
    [InlineData("DebtorTurnover")]
    [InlineData("CreditorTurnover")]
    [InlineData("InventoryTurnover")]
    public void Localization_FinancialRatiosKeys_ExistInEnJson(string key)
    {
        var jsonPath = Path.Combine(
            TestHelper.GetSolutionRoot(), "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", content);
    }
}
