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

}
