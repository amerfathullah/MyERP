using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPackingSlipAppService : IApplicationService
{
    Task<PackingSlipDto> GetAsync(Guid id);
    Task<PagedResultDto<PackingSlipDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PackingSlipDto> CreateAsync(CreatePackingSlipDto input);
    Task<PackingSlipDto> SubmitAsync(Guid id);
    Task<PackingSlipDto> CancelAsync(Guid id);
    Task DeleteAsync(Guid id);
}
