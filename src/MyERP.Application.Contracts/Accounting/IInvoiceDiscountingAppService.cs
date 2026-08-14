using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IInvoiceDiscountingAppService : IApplicationService
{
    Task<PagedResultDto<InvoiceDiscountingDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<InvoiceDiscountingDto> DisburseByIdAsync(Guid id);
    Task<InvoiceDiscountingDto> SettleByIdAsync(Guid id);
    Task<InvoiceDiscountingDto> CalculateAsync(CreateInvoiceDiscountingDto input);
    Task<InvoiceDiscountingDto> DisburseAsync(CreateInvoiceDiscountingDto input);
}
