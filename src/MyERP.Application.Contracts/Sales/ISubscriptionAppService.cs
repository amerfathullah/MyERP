using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISubscriptionAppService : IApplicationService
{
    Task<PagedResultDto<SubscriptionDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<SubscriptionDto> GetAsync(Guid id);
    Task<SubscriptionDto> CreateAsync(CreateSubscriptionDto input);
    Task<SubscriptionDto> CancelAsync(Guid id);
    Task<SubscriptionDto> AdvancePeriodAsync(Guid id);
    Task<GeneratedInvoiceDto> GenerateInvoiceAsync(Guid id);
    Task<List<GeneratedInvoiceDto>> GenerateCatchUpInvoicesAsync(Guid id);
    Task<PlanDimensionsDto> GetPlanDimensionsAsync(Guid itemId, Guid companyId, string? partyType = null);
}
