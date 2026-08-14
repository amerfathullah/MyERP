using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface ISupplierScorecardAppService : IApplicationService
{
    Task<ScorecardDto> GetAsync(Guid id);
    Task<ScorecardDto> GetBySupplierId(Guid supplierId);
    Task<PagedResultDto<ScorecardDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<ScorecardDto> CreateAsync(CreateScorecardDto input);
    Task<ScorecardDto> UpdateScoreAsync(Guid id, decimal newScore);
    Task SubmitPeriodAsync(Guid scorecardId, CreateScorecardPeriodDto input);
    Task DeleteAsync(Guid id);
}
