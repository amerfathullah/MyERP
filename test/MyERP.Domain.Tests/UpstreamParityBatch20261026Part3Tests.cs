using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Parity tests for upstream ERPNext features:
/// - PR #62314 / commit 27303023b9: Value inward transfer leg at what left source warehouse on repost.
/// - PR #61828 / commit 03771aebd8: Reject declaring quotation lost when sales order is made.
/// - PR #61496 / commit 6ddb6d8617: Protect company shareholder (treasury) when submitted share transfers exist.
/// - PR #60523 / commit 1fc15db869: Show ledger balance as account opening balance in bank reconciliation.
/// </summary>
public class UpstreamParityBatch20261026Part3Tests
{
    [Fact]
    public async Task StockValuation_RevaluateFromDateAsync_InwardTransferLegTakesOutwardRate_PR62314()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();

        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var sourceWhId = Guid.NewGuid();
        var targetWhId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        var voucherDetailId = Guid.NewGuid();
        var repostDate = DateTime.UtcNow.AddDays(-5);

        var item = new Item(itemId, companyId, "RAW-MAT", "Raw Material", ItemType.Goods)
        {
            ValuationMethod = ValuationMethod.WeightedAverage
        };
        itemRepo.GetAsync(itemId).Returns(Task.FromResult(item));

        // Outward leg: 2 units left source warehouse with value difference of -20.01m (rate = 10.005)
        var outwardSle = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, sourceWhId,
            repostDate, -2m, 10.005m, 0m, 0m)
        {
            VoucherType = "StockEntry",
            VoucherId = voucherId,
            VoucherDetailNo = voucherDetailId,
            StockValueDifference = -20.01m,
            PostingDateTime = repostDate
        };

        // Inward leg: 2 units entered target warehouse, initially with old rate 10.00m
        var inwardSle = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, targetWhId,
            repostDate, 2m, 10.00m, 2m, 20.00m)
        {
            VoucherType = "StockEntry",
            VoucherId = voucherId,
            VoucherDetailNo = voucherDetailId,
            RecalculateRate = true,
            StockValueDifference = 20.00m,
            PostingDateTime = repostDate
        };

        var allSles = new List<StockLedgerEntry> { outwardSle, inwardSle };
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(allSles.AsQueryable()));

        var valuationService = new StockValuationService(sleRepo, itemRepo, settingProvider);

        // Revaluate target warehouse from repostDate
        await valuationService.RevaluateFromDateAsync(itemId, targetWhId, repostDate.AddDays(-1));

        // The inward leg's incoming rate and valuation rate must be updated to match what left the source (20.01 / 2 = 10.005)
        inwardSle.ValuationRate.ShouldBe(10.005m);
        inwardSle.IncomingRate.ShouldBe(10.005m);
        inwardSle.BalanceValue.ShouldBe(20.01m);
        inwardSle.StockValueDifference.ShouldBe(20.01m);
    }

    [Fact]
    public async Task Quotation_MarkLostAsync_BlocksOrderedQuotationItself_PR61828()
    {
        var quotationRepo = Substitute.For<IRepository<Quotation, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var compDetailRepo = Substitute.For<IRepository<global::MyERP.CRM.Entities.CompetitorDetail, Guid>>();
        var lostDetailRepo = Substitute.For<IRepository<QuotationLostReasonDetail, Guid>>();
        var lostReasonRepo = Substitute.For<IRepository<QuotationLostReason, Guid>>();
        var numberGen = Substitute.For<MyERP.Core.DomainServices.IDocumentNumberGenerator>();
        var pricingRule = Substitute.For<PricingRuleApplicationService>(
            Substitute.For<IRepository<PricingRule, Guid>>(),
            (DiscountCeilingValidationService)null!);

        var quotationId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var quotation = new Quotation(quotationId, companyId, customerId, "QTN-001", DateTime.UtcNow);
        quotation.AddItem(Guid.NewGuid(), "Desk", 2m, 500m, 0m);
        quotation.Submit();
        // Mark quotation itself as having an ordered sales order
        quotation.ConvertedToSalesOrderId = Guid.NewGuid();

        quotationRepo.GetAsync(quotationId).Returns(Task.FromResult(quotation));

        var appService = new QuotationAppService(
            quotationRepo, customerRepo, compDetailRepo, lostDetailRepo, lostReasonRepo, numberGen, pricingRule);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.MarkLostAsync(quotationId, new MarkQuotationLostDto
            {
                LostReasonIds = new List<Guid> { Guid.NewGuid() },
                DetailedReason = "Client bought competitor product"
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldBe("Cannot set as Lost as Sales Order is made.");
    }

    [Fact]
    public async Task Shareholder_DeleteAsync_BlocksCompanyShareholderWithSubmittedTransfers_PR61496()
    {
        var shareholderRepo = Substitute.For<IRepository<Shareholder, Guid>>();
        var transferRepo = Substitute.For<IRepository<ShareTransfer, Guid>>();

        var companyId = Guid.NewGuid();
        var companyShareholderId = Guid.NewGuid();
        var companyShareholder = new Shareholder(companyShareholderId, companyId, "Company Treasury", isCompany: true);

        shareholderRepo.WithDetailsAsync().Returns(Task.FromResult(new List<Shareholder> { companyShareholder }.AsQueryable()));

        // Submitted transfer belonging to this company treasury
        var transfer = new ShareTransfer(
            Guid.NewGuid(),
            companyId,
            ShareTransferType.Issue,
            DateTime.UtcNow,
            Guid.NewGuid(),
            1,
            50,
            10m)
        {
            FromShareholderId = Guid.NewGuid(),
            ToShareholderId = Guid.NewGuid()
        };
        transfer.MarkSubmitted();

        transferRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ShareTransfer> { transfer }.AsQueryable()));

        var appService = new ShareholderAppService(shareholderRepo, transferRepo);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.DeleteAsync(companyShareholderId));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Cannot delete a shareholder with submitted share transfers");
    }

    [Fact]
    public async Task BankReconciliation_GetAccountOpeningBalanceAsync_CalculatesLedgerBalance_PR60523()
    {
        var repo = Substitute.For<IRepository<BankTransaction, Guid>>();
        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var baRepo = Substitute.For<IRepository<BankAccount, Guid>>();
        var accRepo = Substitute.For<IRepository<Account, Guid>>();
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        var jeLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();

        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var glAccountId = Guid.NewGuid();
        var tillDate = DateTime.UtcNow.Date;

        var bankAccount = new BankAccount(bankAccountId, companyId, "Maybank Business", glAccountId, "Maybank");
        baRepo.FindAsync(bankAccountId).Returns(Task.FromResult<BankAccount?>(bankAccount));

        var account = new Account(glAccountId, companyId, "1110", "Bank - MBB", AccountType.Asset)
        {
            Currency = "MYR",
            AccountSubType = AccountSubType.BankAccount
        };
        accRepo.FindAsync(glAccountId).Returns(Task.FromResult<Account?>(account));

        var company = new Company(companyId, "My Company") { CurrencyCode = "MYR" };
        companyRepo.FindAsync(companyId).Returns(Task.FromResult<Company?>(company));

        var offsetAccountId = Guid.NewGuid();

        var fiscalYearId = Guid.NewGuid();
        var je1 = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, tillDate.AddDays(-2));
        je1.AddLine(glAccountId, 12700m, isDebit: true);
        je1.AddLine(offsetAccountId, 12700m, isDebit: false);
        je1.Post();

        var je2 = new JournalEntry(Guid.NewGuid(), companyId, fiscalYearId, tillDate.AddDays(-1));
        je2.AddLine(offsetAccountId, 240m, isDebit: true);
        je2.AddLine(glAccountId, 240m, isDebit: false);
        je2.Post();

        jeRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry> { je1, je2 }.AsQueryable()));

        var allLines = je1.Lines.Concat(je2.Lines).ToList();
        jeLineRepo.GetQueryableAsync().Returns(Task.FromResult(allLines.AsQueryable()));

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<BankAccount, Guid>>().Returns(baRepo);
        lazySp.LazyGetRequiredService<IRepository<Account, Guid>>().Returns(accRepo);
        lazySp.LazyGetRequiredService<IRepository<Company, Guid>>().Returns(companyRepo);
        lazySp.LazyGetRequiredService<IRepository<JournalEntry, Guid>>().Returns(jeRepo);
        lazySp.LazyGetRequiredService<IRepository<JournalEntryLine, Guid>>().Returns(jeLineRepo);

        var appService = new BankReconciliationAppService(repo, peRepo, jeRepo, baRepo, null!, null!)
        {
            LazyServiceProvider = lazySp
        };

        var openingBalance = await appService.GetAccountOpeningBalanceAsync(bankAccountId, tillDate);

        // 12,700 debit - 240 credit = 12,460 balance (matches ERPNext test_opening_balance_includes_uncleared_entries)
        openingBalance.ShouldBe(12460m);
    }
}
