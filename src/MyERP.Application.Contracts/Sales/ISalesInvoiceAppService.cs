using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesInvoiceAppService : IApplicationService
{
    Task<SalesInvoiceDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesInvoiceDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<SalesInvoiceDto> CreateAsync(CreateSalesInvoiceDto input);
    Task<SalesInvoiceDto> SubmitAsync(Guid id);
    Task<BulkOperationResultDto> BulkSubmitAsync(List<Guid> ids);
    Task<SalesInvoiceDto> PostAsync(Guid id);
    Task<BulkOperationResultDto> BulkPostAsync(List<Guid> ids);
    Task<SalesInvoiceDto> CancelAsync(Guid id);
    Task<SalesInvoiceDto> WriteOffAsync(Guid id);
    Task<SalesInvoiceDto> AmendAsync(Guid id);
    Task<SalesInvoiceDto> CreateDebitNoteAsync(Guid salesInvoiceId);
    Task DeleteAsync(Guid id);
    Task<List<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid invoiceId);
    Task<List<InvoicePaymentHistoryDto>> GetPaymentHistoryAsync(Guid invoiceId);

    /// <summary>
    /// Creates a single Sales Invoice from multiple Delivery Notes for the same customer.
    /// Per ERPNext: primary billing workflow for goods-based businesses that deliver daily but invoice weekly/monthly.
    /// </summary>
    Task<SalesInvoiceDto> CreateFromDeliveryNotesAsync(CreateInvoiceFromDeliveryNotesDto input);

    /// <summary>
    /// Returns aggregate KPI summary for the SI list page: outstanding, overdue, monthly revenue.
    /// Enables dashboard-style cards without fetching all invoices.
    /// </summary>
    Task<SalesInvoiceListSummaryDto> GetListSummaryAsync(Guid? companyId);
}
