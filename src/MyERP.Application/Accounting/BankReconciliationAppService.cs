using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class BankReconciliationAppService : ApplicationService, IBankReconciliationAppService
{
    private readonly IRepository<BankTransaction, Guid> _repository;
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;
    private readonly BankAutoMatchService _autoMatchService;
    private readonly BankInternalTransferService _internalTransferService;

    public BankReconciliationAppService(
        IRepository<BankTransaction, Guid> repository,
        IRepository<PaymentEntry, Guid> paymentEntryRepository,
        BankAutoMatchService autoMatchService,
        BankInternalTransferService internalTransferService)
    {
        _repository = repository;
        _paymentEntryRepository = paymentEntryRepository;
        _autoMatchService = autoMatchService;
        _internalTransferService = internalTransferService;
    }

    public async Task<PagedResultDto<BankTransactionDto>> GetTransactionsAsync(GetBankTransactionsDto input)
    {
        var query = await _repository.GetQueryableAsync();
        query = query.Where(t => t.BankAccountId == input.BankAccountId);

        if (input.IsReconciled.HasValue)
            query = query.Where(t => t.IsReconciled == input.IsReconciled.Value);
        if (input.DateFrom.HasValue)
            query = query.Where(t => t.TransactionDate >= input.DateFrom.Value);
        if (input.DateTo.HasValue)
            query = query.Where(t => t.TransactionDate <= input.DateTo.Value);

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(t => t.TransactionDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<BankTransactionDto>(
            totalCount,
            items.Select(ObjectMapper.Map<BankTransaction, BankTransactionDto>).ToList());
    }

    public async Task<BankTransactionDto> ReconcileAsync(ReconcileBankTransactionDto input)
    {
        var tx = await _repository.GetAsync(input.TransactionId);
        tx.Reconcile(input.PaymentEntryId, input.MatchedDocumentRef);
        await _repository.UpdateAsync(tx);
        return ObjectMapper.Map<BankTransaction, BankTransactionDto>(tx);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Default)]
    public async Task<BankTransactionDto> UnreconcileAsync(Guid id)
    {
        var tx = await _repository.GetAsync(id);
        tx.Unreconcile();
        await _repository.UpdateAsync(tx);
        return ObjectMapper.Map<BankTransaction, BankTransactionDto>(tx);
    }

    public async Task<BankTransactionDto> ImportTransactionAsync(ImportBankTransactionDto input)
    {
        var tx = new BankTransaction(
            GuidGenerator.Create(),
            input.CompanyId,
            input.BankAccountId,
            input.TransactionDate,
            input.Description,
            input.Amount,
            CurrentTenant.Id)
        {
            ReferenceNumber = input.ReferenceNumber
        };

        await _repository.InsertAsync(tx);
        return ObjectMapper.Map<BankTransaction, BankTransactionDto>(tx);
    }

    public async Task<AutoMatchResultDto> AutoMatchAsync(Guid bankAccountId, Guid companyId)
    {
        var result = await _autoMatchService.AutoMatchAsync(bankAccountId, companyId);
        return new AutoMatchResultDto
        {
            MatchedCount = result.MatchedCount,
            PartiallyReconciledCount = result.PartiallyReconciledCount,
            UnmatchedCount = result.UnmatchedCount,
        };
    }

    public async Task<BankReconciliationSummaryDto> GetSummaryAsync(Guid bankAccountId)
    {
        var query = await _repository.GetQueryableAsync();
        var txs = query.Where(t => t.BankAccountId == bankAccountId).ToList();

        return new BankReconciliationSummaryDto
        {
            TotalTransactions = txs.Count,
            ReconciledCount = txs.Count(t => t.IsReconciled),
            UnreconciledCount = txs.Count(t => !t.IsReconciled),
            TotalDeposits = txs.Where(t => t.Amount > 0).Sum(t => t.Amount),
            TotalWithdrawals = txs.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
            UnreconciledBalance = txs.Where(t => !t.IsReconciled).Sum(t => t.Amount),
        };
    }

    /// <summary>
    /// Returns ranked match candidates for manual reconciliation.
    /// Per ERPNext: manual matching shows all candidates sorted by composite rank.
    /// </summary>
    public async Task<List<MatchCandidateDto>> GetMatchCandidatesAsync(Guid bankTransactionId, Guid companyId)
    {
        var candidates = await _autoMatchService.GetMatchCandidatesAsync(bankTransactionId, companyId);
        return candidates.Select(c => new MatchCandidateDto
        {
            PaymentEntryId = c.PaymentEntryId,
            PaymentNumber = c.PaymentNumber,
            Amount = c.Amount,
            PostingDate = c.PostingDate,
            ReferenceNumber = c.ReferenceNumber,
            Rank = c.Rank,
        }).ToList();
    }

    /// <summary>
    /// Searches for a mirror bank transaction (opposite-sign in different bank account).
    /// Per ERPNext search_for_transfer_transaction: ±3 days, exactly 1 match required.
    /// </summary>
    public async Task<MirrorTransactionDto?> SearchForMirrorTransactionAsync(Guid transactionId)
    {
        var result = await _internalTransferService.SearchForMirrorTransactionAsync(transactionId);
        if (result == null) return null;

        return new MirrorTransactionDto
        {
            TransactionId = result.TransactionId,
            BankAccountId = result.BankAccountId,
            ReferenceNumber = result.ReferenceNumber,
            TransactionDate = result.TransactionDate,
            Deposit = result.Deposit,
            Withdrawal = result.Withdrawal,
            CurrencyCode = result.CurrencyCode,
        };
    }

    /// <summary>
    /// Creates an Internal Transfer Payment Entry from a bank transaction.
    /// Per ERPNext create_internal_transfer: creates PE, reconciles source + mirror.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<InternalTransferResultDto> CreateInternalTransferAsync(CreateInternalTransferDto input)
    {
        var tx = await _repository.GetAsync(input.BankTransactionId);
        var sourceBankGlId = tx.BankAccountId; // In production, resolve GL account from Bank Account entity

        var spec = _internalTransferService.BuildInternalTransfer(
            tx, sourceBankGlId, input.TargetBankAccountGlId, input.CompanyId);

        // Create the Internal Transfer Payment Entry
        var pe = new PaymentEntry(
            GuidGenerator.Create(),
            spec.CompanyId,
            PaymentType.InternalTransfer,
            spec.PostingDate,
            spec.Amount,
            spec.PaidFromAccountId,
            spec.PaidToAccountId,
            CurrentTenant.Id)
        {
            ReferenceNumber = spec.ReferenceNumber,
            PaymentNumber = $"PE-IT-{DateTime.UtcNow:yyyyMMddHHmmss}",
        };

        pe.Submit();
        pe.Post();
        await _paymentEntryRepository.InsertAsync(pe);

        // Reconcile both sides (source + mirror)
        await _internalTransferService.ReconcileBothSidesAsync(
            input.BankTransactionId,
            input.MirrorTransactionId,
            pe.Id,
            pe.PaymentNumber);

        return new InternalTransferResultDto
        {
            PaymentEntryId = pe.Id,
            PaymentNumber = pe.PaymentNumber,
            SourceTransactionId = input.BankTransactionId,
            MirrorTransactionId = input.MirrorTransactionId,
        };
    }

    /// <summary>
    /// Creates a Payment Entry directly from a bank transaction and auto-reconciles it.
    /// Per ERPNext banking module (gotcha #784): sets reconciliation_type = "Voucher Created".
    /// Deposit transactions → Receive type PE (money in from customer).
    /// Withdrawal transactions → Pay type PE (money out to supplier).
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<VoucherCreatedResultDto> CreatePaymentEntryFromTransactionAsync(CreatePEFromTransactionDto input)
    {
        var tx = await _repository.GetAsync(input.BankTransactionId);

        if (tx.IsReconciled)
        {
            throw new Volo.Abp.BusinessException("MyERP:02048")
                .WithData("transactionId", tx.Id);
        }

        // Determine payment type from transaction direction
        // Positive amount (deposit) = money received → Receive type
        // Negative amount (withdrawal) = money paid → Pay type
        var isDeposit = tx.Amount > 0;
        var paymentType = isDeposit ? PaymentType.Receive : PaymentType.Pay;
        var amount = Math.Abs(tx.Amount);

        // Resolve paid-from and paid-to based on direction:
        // Receive: paid_from = party account (customer), paid_to = bank account
        // Pay: paid_from = bank account, paid_to = party account (supplier)
        var paidFromAccountId = isDeposit ? input.PartyAccountId : input.BankAccountId;
        var paidToAccountId = isDeposit ? input.BankAccountId : input.PartyAccountId;

        // Generate PE number
        var numberGenerator = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.IDocumentNumberGenerator>();
        var peNumber = await numberGenerator.GenerateAsync("PE", input.CompanyId, tx.TransactionDate);

        // Create the Payment Entry
        var pe = new PaymentEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            paymentType,
            tx.TransactionDate,
            amount,
            paidFromAccountId,
            paidToAccountId,
            CurrentTenant.Id)
        {
            PaymentNumber = peNumber,
            ReferenceNumber = tx.ReferenceNumber,
            PartyType = input.PartyType,
            PartyId = input.PartyId,
            ModeOfPaymentId = input.ModeOfPaymentId,
            CurrencyCode = tx.CurrencyCode ?? "MYR",
        };

        // Link to invoice if specified (enables outstanding reduction on post)
        if (input.AgainstInvoiceId.HasValue)
        {
            pe.AgainstInvoiceId = input.AgainstInvoiceId.Value;
            pe.AgainstInvoiceType = input.PartyType == "Customer" ? "SalesInvoice" : "PurchaseInvoice";
        }

        // Submit + Post the PE atomically (bank reconciliation creates posted entries)
        pe.Submit();
        pe.Post();
        await _paymentEntryRepository.InsertAsync(pe);

        // Auto-reconcile: link bank transaction to the newly created PE
        tx.Reconcile(pe.Id, pe.PaymentNumber);
        await _repository.UpdateAsync(tx);

        // Audit trail
        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PaymentEntry", pe.Id, "Posted",
            input.CompanyId, peNumber, "Draft", "Posted",
            CurrentUser.Id, details: $"Created from bank transaction: {tx.Description}",
            tenantId: CurrentTenant.Id));

        return new VoucherCreatedResultDto
        {
            PaymentEntryId = pe.Id,
            PaymentNumber = peNumber,
            Amount = amount,
            PaymentType = paymentType.ToString(),
            BankTransactionId = tx.Id,
            IsReconciled = true,
        };
    }

    /// <summary>
    /// Bank Reconciliation Statement: GL balance - uncleared items = expected bank balance.
    /// Per ERPNext Bank Reconciliation Statement report logic.
    /// </summary>
    public async Task<BankReconciliationStatementDto> GetReconciliationStatementAsync(
        GetBankReconciliationStatementInput input)
    {
        // Step 1: Get bank GL account from Account entity
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        var account = await accountRepo.FindAsync(input.BankAccountId);
        var accountName = account?.AccountName ?? "Bank Account";

        // Step 2: Calculate GL balance for the bank account as of report date
        // Sum all posted JE lines hitting this account up to report date
        var jeLineRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntryLine, Guid>>();
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntry, Guid>>();

        var postedJeIds = (await jeRepo.GetQueryableAsync())
            .Where(je => je.CompanyId == input.CompanyId
                && je.Status == Core.DocumentStatus.Posted
                && je.PostingDate <= input.ReportDate)
            .Select(je => je.Id)
            .ToList();

        var lines = (await jeLineRepo.GetQueryableAsync())
            .Where(l => postedJeIds.Contains(l.JournalEntryId) && l.AccountId == input.BankAccountId)
            .ToList();

        var totalDebit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        var totalCredit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        var glBalance = totalDebit - totalCredit;

        // Step 3: Find uncleared bank transactions (posted payments not yet cleared at bank)
        // These are payments/receipts that hit GL but haven't been reconciled with bank statement
        var peQuery = await _paymentEntryRepository.GetQueryableAsync();
        var unclearedPEs = peQuery
            .Where(pe => pe.CompanyId == input.CompanyId
                && pe.Status == Core.DocumentStatus.Posted
                && pe.PostingDate <= input.ReportDate
                && (pe.PaidFromAccountId == input.BankAccountId || pe.PaidToAccountId == input.BankAccountId))
            .ToList();

        // Filter to only those NOT reconciled (no matching reconciled bank transaction)
        var reconciledPeIds = (await _repository.GetQueryableAsync())
            .Where(bt => bt.IsReconciled && bt.PaymentEntryId.HasValue)
            .Select(bt => bt.PaymentEntryId!.Value)
            .ToHashSet();

        var unclearedEntries = new List<BankStatementEntryDto>();
        decimal outstandingDeposits = 0;
        decimal outstandingPayments = 0;

        foreach (var pe in unclearedPEs.Where(pe => !reconciledPeIds.Contains(pe.Id)))
        {
            // Determine direction: money INTO bank (deposit) or OUT of bank (payment)
            var isDeposit = pe.PaidToAccountId == input.BankAccountId;
            var amount = pe.PaidAmount;

            if (isDeposit)
                outstandingDeposits += amount;
            else
                outstandingPayments += amount;

            unclearedEntries.Add(new BankStatementEntryDto
            {
                PostingDate = pe.PostingDate,
                DocumentType = "Payment Entry",
                DocumentNumber = pe.PaymentNumber ?? pe.Id.ToString()[..8],
                DocumentId = pe.Id,
                Debit = isDeposit ? amount : 0,
                Credit = isDeposit ? 0 : amount,
                ReferenceNumber = pe.ReferenceNumber,
                ClearanceDate = null,
                PartyName = null
            });
        }

        return new BankReconciliationStatementDto
        {
            GlBalance = glBalance,
            OutstandingDeposits = outstandingDeposits,
            OutstandingPayments = outstandingPayments,
            UnclearedEntries = unclearedEntries.OrderBy(e => e.PostingDate).ToList(),
            CurrencyCode = account?.Currency ?? "MYR",
            ReportDate = input.ReportDate,
            BankAccountName = accountName
        };
    }

}
