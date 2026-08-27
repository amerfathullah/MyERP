using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBisectAccountingStatementsAppService : IApplicationService
{
    Task<PagedResultDto<BisectAccountingStatementsDto>> GetListAsync(BisectAccountingStatementsGetListInput input);
    Task<BisectAccountingStatementsDto> GetAsync(Guid id);
    Task<BisectAccountingStatementsDto> CreateAndBuildTreeAsync(CreateBisectAccountingStatementsDto input);
    Task<BisectAccountingStatementsDto> BisectLeftAsync(Guid id);
    Task<BisectAccountingStatementsDto> BisectRightAsync(Guid id);
    Task<BisectAccountingStatementsDto> MoveUpAsync(Guid id);
    Task DeleteAsync(Guid id);
}
