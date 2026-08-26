using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Automation;

public interface IBulkTransactionLogAppService : IApplicationService
{
    Task<BulkTransactionLogDto> GetAsync(Guid id);
    Task<PagedResultDto<BulkTransactionLogDto>> GetListAsync(GetBulkTransactionLogListDto input);
    Task<BulkTransactionLogDto> CreateAsync(CreateBulkTransactionLogDto input);
    Task<BulkTransactionLogDto> RecordDetailResultAsync(Guid id, Guid detailId, RecordBulkTransactionResultDto input);
    Task<BulkTransactionLogDto> RetryDetailAsync(Guid id, Guid detailId);
    Task DeleteAsync(Guid id);
}
