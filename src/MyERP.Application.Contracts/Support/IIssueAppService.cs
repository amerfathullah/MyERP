using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Support;

/// <summary>Issue application service interface.</summary>
public interface IIssueAppService : IApplicationService
{
    Task<IssueDto> GetAsync(Guid id);
    Task<PagedResultDto<IssueDto>> GetListAsync(GetIssueListDto input);
    Task<IssueDto> CreateAsync(CreateIssueDto input);
    Task<IssueDto> ReplyAsync(Guid id);
    Task<IssueDto> ResolveAsync(Guid id, ResolveIssueDto input);
    Task<IssueDto> ReopenAsync(Guid id);
    Task<IssueDto> HoldAsync(Guid id);
    Task<IssueDto> SplitAsync(Guid id, SplitIssueDto input);
}
