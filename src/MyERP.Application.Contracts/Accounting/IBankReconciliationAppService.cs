using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankReconciliationAppService : IApplicationService
{
    Task<PagedResultDto<BankTransactionDto>> GetTransactionsAsync(GetBankTransactionsDto input);
    Task<BankTransactionDto> ReconcileAsync(ReconcileBankTransactionDto input);
    Task<BankTransactionDto> UnreconcileAsync(Guid id);
    Task<BankTransactionDto> ImportTransactionAsync(ImportBankTransactionDto input);
    Task<BankReconciliationSummaryDto> GetSummaryAsync(Guid bankAccountId);
}

public class BankTransactionDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public bool IsReconciled { get; set; }
    public Guid? PaymentEntryId { get; set; }
    public string? MatchedDocumentRef { get; set; }
    public DateTime? ReconciledAt { get; set; }
}

public class GetBankTransactionsDto : PagedAndSortedResultRequestDto
{
    [Required]
    public Guid BankAccountId { get; set; }

    public bool? IsReconciled { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class ReconcileBankTransactionDto
{
    [Required]
    public Guid TransactionId { get; set; }

    [Required]
    public Guid PaymentEntryId { get; set; }

    public string? MatchedDocumentRef { get; set; }
}

public class ImportBankTransactionDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid BankAccountId { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    [Required]
    public string Description { get; set; } = null!;

    [Required]
    public decimal Amount { get; set; }

    public string? ReferenceNumber { get; set; }
}

public class BankReconciliationSummaryDto
{
    public int TotalTransactions { get; set; }
    public int ReconciledCount { get; set; }
    public int UnreconciledCount { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal UnreconciledBalance { get; set; }
}
