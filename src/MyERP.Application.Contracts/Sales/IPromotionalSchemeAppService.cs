using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPromotionalSchemeAppService : IApplicationService
{
    Task<PagedResultDto<PromotionalSchemeDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PromotionalSchemeDto> GetAsync(Guid id);
    Task<PromotionalSchemeDto> CreateAsync(CreateUpdatePromotionalSchemeDto input);
    Task<PromotionalSchemeDto> UpdateAsync(Guid id, CreateUpdatePromotionalSchemeDto input);
    Task DeleteAsync(Guid id);
}
