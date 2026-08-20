using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IInvoiceDiscountingAppService : IApplicationService
{
    Task<PagedResultDto<InvoiceDiscountingDto>> GetListAsync(PagedAndSortedResultRequestDto input, Guid? companyId = null);
    Task<InvoiceDiscountingDto> GetAsync(Guid id);

    /// <summary>Sales Invoices eligible to be newly pledged (posted, has outstanding, not already pledged elsewhere).</summary>
    Task<List<InvoiceForDiscountingDto>> GetEligibleInvoicesAsync(Guid companyId, Guid? customerId = null);

    Task<DiscountingCalculationResultDto> CalculateAsync(CalculateDiscountingDto input);

    Task<InvoiceDiscountingDto> CreateAsync(CreateInvoiceDiscountingDto input);

    /// <summary>Draft -&gt; Sanctioned. Posts the AR-swap Journal Entry moving each invoice's receivable to the holding account.</summary>
    Task<InvoiceDiscountingDto> SubmitAsync(Guid id, SubmitInvoiceDiscountingDto input);

    /// <summary>Sanctioned -&gt; Disbursed. Posts the loan-disbursement Journal Entry (bank pays out).</summary>
    Task<InvoiceDiscountingDto> DisburseAsync(Guid id, DisburseInvoiceDiscountingDto input);

    /// <summary>Disbursed -&gt; Settled. Posts the loan-settlement Journal Entry (loan repaid).</summary>
    Task<InvoiceDiscountingDto> SettleAsync(Guid id);

    /// <summary>Cancels from Draft/Sanctioned/Disbursed, reversing whichever GL entries were posted so far.</summary>
    Task<InvoiceDiscountingDto> CancelAsync(Guid id);
}
