using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface ITermsAndConditionsAppService : IApplicationService
{
    Task<TermsAndConditionsDto> GetAsync(Guid id);
    Task<PagedResultDto<TermsAndConditionsDto>> GetListAsync(GetTermsAndConditionsListDto input);
    Task<TermsAndConditionsDto> CreateAsync(CreateUpdateTermsAndConditionsDto input);
    Task<TermsAndConditionsDto> UpdateAsync(Guid id, CreateUpdateTermsAndConditionsDto input);
    Task DeleteAsync(Guid id);
}
