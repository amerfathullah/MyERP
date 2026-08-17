using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Support;

public interface IIssueTypeAppService : IApplicationService
{
    Task<IssueTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<IssueTypeDto>> GetListAsync(GetIssueTypeListDto input);
    Task<IssueTypeDto> CreateAsync(CreateUpdateIssueTypeDto input);
    Task<IssueTypeDto> UpdateAsync(Guid id, CreateUpdateIssueTypeDto input);
    Task DeleteAsync(Guid id);
}
