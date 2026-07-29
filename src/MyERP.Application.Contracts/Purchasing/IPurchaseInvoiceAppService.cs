using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Sales;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseInvoiceAppService : IApplicationService
{
    Task<PurchaseInvoiceDto> GetAsync(Guid id);
    Task<PagedResultDto<PurchaseInvoiceDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PurchaseInvoiceDto> CreateAsync(CreatePurchaseInvoiceDto input);
    Task<PurchaseInvoiceDto> UpdateAsync(Guid id, CreatePurchaseInvoiceDto input);
    Task<PurchaseInvoiceDto> SubmitAsync(Guid id);
    Task<PurchaseInvoiceDto> PostAsync(Guid id);
    Task<PurchaseInvoiceDto> CancelAsync(Guid id);
    Task<PurchaseInvoiceDto> WriteOffAsync(Guid id);
    Task<PurchaseInvoiceDto> AmendAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<List<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid invoiceId);
    Task<List<ThreeWayMatchingItemDto>> GetThreeWayMatchingAsync(Guid invoiceId);
    Task<List<TaxWithholdingEntryDto>> GetTaxWithholdingEntriesAsync(Guid invoiceId);

    /// <summary>
    /// Returns aggregate KPI summary for the PI list page: total payables, overdue, monthly spend.
    /// </summary>
    Task<PurchaseInvoiceListSummaryDto> GetListSummaryAsync(Guid? companyId);
}
