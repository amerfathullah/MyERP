using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesPartnerAppService : IApplicationService
{
    Task<PagedResultDto<SalesPartnerDto>> GetListAsync(GetSalesPartnerListDto input);
    Task<SalesPartnerDto> GetAsync(Guid id);
    Task<SalesPartnerDto> CreateAsync(CreateSalesPartnerDto input);
    Task<SalesPartnerDto> UpdateAsync(Guid id, CreateSalesPartnerDto input);
    Task DeleteAsync(Guid id);
    Task ToggleAsync(Guid id);
    Task<SalesPartnerDto> AddTargetAsync(Guid id, CreateSalesPartnerTargetDto input);
    Task<SalesPartnerDto> RemoveTargetAsync(Guid id, Guid targetId);

    /// <summary>
    /// Gets public list of published sales partners for website visitors (/partners).
    /// Per ERPNext PR #64708 / commit ec0e080942: allows guest access and excludes internal commission/target data.
    /// </summary>
    Task<PagedResultDto<PublicSalesPartnerDto>> GetPublishedPartnersAsync(PagedAndSortedResultRequestDto input);
}
