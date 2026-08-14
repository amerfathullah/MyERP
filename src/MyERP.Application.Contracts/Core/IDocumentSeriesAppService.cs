using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IDocumentSeriesAppService : IApplicationService
{
    Task<PagedResultDto<DocumentSeriesDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<DocumentSeriesDto> CreateAsync(CreateDocumentSeriesDto input);
}
