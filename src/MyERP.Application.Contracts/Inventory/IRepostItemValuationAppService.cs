using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IRepostItemValuationAppService : IApplicationService
{
    Task<PagedResultDto<RepostItemValuationDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<RepostItemValuationDto> GetAsync(Guid id);
    Task<RepostItemValuationDto> CreateAsync(CreateRepostItemValuationDto input);
    Task<int> GetPendingCountAsync(Guid companyId);
    Task<RepostItemValuationDto> RestartAsync(Guid id);
    Task<RepostItemValuationDto> CancelAsync(Guid id);
    Task<RepostItemValuationSummaryDto> GetSummaryAsync(Guid companyId);
}
