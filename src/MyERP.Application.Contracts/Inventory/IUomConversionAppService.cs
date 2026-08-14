using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IUomConversionAppService : IApplicationService
{
    Task<PagedResultDto<UomConversionDto>> GetListAsync(PagedAndSortedResultRequestDto input);
}
