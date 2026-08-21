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

    /// <summary>
    /// Computes the next suggested case number for a Delivery Note: MAX(ToCaseNo) + 1, or 1 if none.
    /// Per ERPNext packing_slip.py (gotcha #128).
    /// </summary>
    Task<int> GetNextCaseNoAsync(Guid deliveryNoteId);
}
