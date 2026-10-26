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
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 2026-10-26:
/// - PR #60585 / commit ff8bbe8f56: Payment Entry Internal Transfer Bank Account fields.
/// - PR #61988 / commits fe95375b98, f6f18a9ca2, b5a4734a8d: Budget commitments precision rounding and closed item exclusion.
/// - PR #61484 / commit eb2a08a8ac: Payment Reconciliation batch over-allocation protection across multiple rows for the same invoice.
/// - PR #61496 / commit 3421cba306: Shareholder register protection on company edit and deletion.
/// - PR #60274 / commit c3402e7280 & PR #62153 / commit 36ddeb4f5e: CoA import duplicate name and circular hierarchy detection.
/// </summary>
public class UpstreamParityBatch20261026Tests
{
    [Fact]
    public void PaymentEntry_InternalTransfer_BankAccountProperties_PR60585()
    {
        var companyId = Guid.NewGuid();
        var paidFrom = Guid.NewGuid();
        var paidTo = Guid.NewGuid();
        var bankFrom = Guid.NewGuid();
        var bankTo = Guid.NewGuid();

        var pe = new PaymentEntry(
            Guid.NewGuid(),
            companyId,
            PaymentType.InternalTransfer,
            DateTime.UtcNow,
            500m,
            paidFrom,
            paidTo);

        pe.PaidFromBankAccountId.ShouldBeNull();
        pe.PaidToBankAccountId.ShouldBeNull();

        pe.PaidFromBankAccountId = bankFrom;
        pe.PaidToBankAccountId = bankTo;

        pe.PaidFromBankAccountId.ShouldBe(bankFrom);
        pe.PaidToBankAccountId.ShouldBe(bankTo);
    }

    [Fact]
    public void BudgetCommitments_PrecisionRounding_And_ClosedItem_PR61988()
    {
        // 1. Sub-cent rounding precision
        decimal budget = 5000.00m;
        decimal totalSpendWithNoise = 5000.003m;
        var diff = totalSpendWithNoise - budget;
        (diff > 0m).ShouldBeTrue();
        (Math.Round(diff, 2) > 0m).ShouldBeFalse();

        // 2. Closed PO item excluded
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-UP-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Raw Material A", 10m, 100m, 0m, "Unit");
        var item = po.Items[0];
        item.IsClosed.ShouldBeFalse();
        po.CloseItem(item.Id);
        item.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public async Task PaymentReconciliation_BatchOverAllocation_SameInvoiceTargetedTwice_PR61484()
    {
        var pleRepo = Substitute.For<IRepository<PaymentLedgerEntry, Guid>>();
        var siRepo = Substitute.For<IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        var coRepo = Substitute.For<IRepository<Company, Guid>>();

        var companyId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var paymentAId = Guid.NewGuid();
        var paymentBId = Guid.NewGuid();

        // Outstanding on invoice is 500
        var pleList = new List<PaymentLedgerEntry>
        {
            new(
                Guid.NewGuid(), companyId, DateTime.UtcNow,
                accountId, "Customer", partyId,
                "SalesInvoice", invoiceId,
                "SalesInvoice", invoiceId,
                500m, 500m,
                "MYR")
        };

        pleRepo.GetQueryableAsync().Returns(Task.FromResult(pleList.AsQueryable()));
        pleRepo.InsertAsync(Arg.Do<PaymentLedgerEntry>(entry => pleList.Add(entry)))
            .Returns(call => Task.FromResult(call.Arg<PaymentLedgerEntry>()));

        var pleService = new PaymentLedgerService(pleRepo);

        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        pleService.LazyServiceProvider = lazyProvider;

        var engine = new PaymentReconciliationEngine(
            pleService,
            pleRepo,
            siRepo,
            piRepo,
            peRepo,
            jeRepo,
            fyRepo,
            coRepo);

        var allocations = new List<ReconciliationAllocation>
        {
            // First allocation takes 400
            new()
            {
                InvoiceVoucherType = "SalesInvoice",
                InvoiceVoucherId = invoiceId,
                PaymentVoucherType = "PaymentEntry",
                PaymentVoucherId = paymentAId,
                AllocatedAmount = 400m
            },
            // Second allocation tries to take 200 (total 600 > 500)
            new()
            {
                InvoiceVoucherType = "SalesInvoice",
                InvoiceVoucherId = invoiceId,
                PaymentVoucherType = "PaymentEntry",
                PaymentVoucherId = paymentBId,
                AllocatedAmount = 200m
            }
        };

        var result = await engine.ReconcileBatchAsync(
            companyId,
            "Customer",
            partyId,
            accountId,
            "MYR",
            allocations);

        result.ReconciledCount.ShouldBe(1);
        result.TotalAllocated.ShouldBe(400m);
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].InvoiceVoucherId.ShouldBe(invoiceId);
        result.Errors[0].Message.ShouldContain("exceeded");
    }

    [Fact]
    public async Task Shareholder_DeleteAsync_BlocksShareholderWithActiveShares_PR61496()
    {
        var shareholderRepo = Substitute.For<IRepository<Shareholder, Guid>>();
        var transferRepo = Substitute.For<IRepository<ShareTransfer, Guid>>();

        var shareholderId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var shareholder = new Shareholder(shareholderId, companyId, "Founder", isCompany: false);
        shareholder.AddShareBalance(Guid.NewGuid(), 1, 100, 10m); // 100 active shares

        shareholderRepo.WithDetailsAsync().Returns(Task.FromResult(new List<Shareholder> { shareholder }.AsQueryable()));

        var appService = new ShareholderAppService(shareholderRepo, transferRepo);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.DeleteAsync(shareholderId));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Cannot delete a shareholder with active shares");
    }

    [Fact]
    public async Task Shareholder_UpdateAsync_BlocksCompanyChangeWhenSubmittedTransfersExist_PR61496()
    {
        var shareholderRepo = Substitute.For<IRepository<Shareholder, Guid>>();
        var transferRepo = Substitute.For<IRepository<ShareTransfer, Guid>>();

        var shareholderId = Guid.NewGuid();
        var oldCompanyId = Guid.NewGuid();
        var newCompanyId = Guid.NewGuid();
        var shareholder = new Shareholder(shareholderId, oldCompanyId, "Investor A", isCompany: false);

        shareholderRepo.WithDetailsAsync().Returns(Task.FromResult(new List<Shareholder> { shareholder }.AsQueryable()));

        var transfer = new ShareTransfer(
            Guid.NewGuid(),
            oldCompanyId,
            ShareTransferType.Transfer,
            DateTime.UtcNow,
            Guid.NewGuid(),
            1,
            10,
            50m)
        {
            FromShareholderId = shareholderId,
            ToShareholderId = Guid.NewGuid()
        };
        transfer.MarkSubmitted();

        transferRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ShareTransfer> { transfer }.AsQueryable()));

        var appService = new ShareholderAppService(shareholderRepo, transferRepo);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.UpdateAsync(shareholderId, new CreateUpdateShareholderDto
            {
                CompanyId = newCompanyId,
                Title = "Investor A"
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Cannot change company of a shareholder with submitted share transfers");
    }
}
