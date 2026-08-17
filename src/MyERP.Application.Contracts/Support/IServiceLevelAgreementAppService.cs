using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Support;

public interface IServiceLevelAgreementAppService : IApplicationService
{
    Task<ServiceLevelAgreementDto> GetAsync(Guid id);
    Task<PagedResultDto<ServiceLevelAgreementDto>> GetListAsync(GetServiceLevelAgreementListDto input);
    Task<ServiceLevelAgreementDto> CreateAsync(CreateServiceLevelAgreementDto input);
    Task<ServiceLevelAgreementDto> UpdateAsync(Guid id, CreateServiceLevelAgreementDto input);
    Task DeleteAsync(Guid id);
}
