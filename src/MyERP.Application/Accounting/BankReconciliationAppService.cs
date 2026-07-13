using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class BankReconciliationAppService : ApplicationService, IBankReconciliationAppService
{
    private readonly IRepository<BankTransaction, Guid> _repository;
    private readonly BankAutoMatchService _autoMatchService;

    public BankReconciliationAppService(
        IRepository<BankTransaction, Guid> repository,
        BankAutoMatchService autoMatchService)
    {
        _repository = repository;
        _autoMatchService = autoMatchService;
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
            items.Select(MapToDto).ToList());
    }

    public async Task<BankTransactionDto> ReconcileAsync(ReconcileBankTransactionDto input)
    {
        var tx = await _repository.GetAsync(input.TransactionId);
        tx.Reconcile(input.PaymentEntryId, input.MatchedDocumentRef);
        await _repository.UpdateAsync(tx);
        return MapToDto(tx);
    }

    public async Task<BankTransactionDto> UnreconcileAsync(Guid id)
    {
        var tx = await _repository.GetAsync(id);
        tx.Unreconcile();
        await _repository.UpdateAsync(tx);
        return MapToDto(tx);
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
        return MapToDto(tx);
    }

    public async Task<AutoMatchResultDto> AutoMatchAsync(Guid bankAccountId, Guid companyId)
    {
        var result = await _autoMatchService.AutoMatchAsync(bankAccountId, companyId);
        return new AutoMatchResultDto
        {
            MatchedCount = result.MatchedCount,
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

    private static BankTransactionDto MapToDto(BankTransaction tx) => new()
    {
        Id = tx.Id,
        CompanyId = tx.CompanyId,
        BankAccountId = tx.BankAccountId,
        TransactionDate = tx.TransactionDate,
        Description = tx.Description,
        Amount = tx.Amount,
        ReferenceNumber = tx.ReferenceNumber,
        IsReconciled = tx.IsReconciled,
        PaymentEntryId = tx.PaymentEntryId,
        MatchedDocumentRef = tx.MatchedDocumentRef,
        ReconciledAt = tx.ReconciledAt,
    };
}
