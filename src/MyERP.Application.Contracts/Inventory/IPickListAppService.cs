using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IPickListAppService : IApplicationService
{
    Task<PagedResultDto<PickListDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<PickListDto> GetAsync(Guid id);
    Task<PickListDto> CreateAsync(CreatePickListDto input);
    Task<PickListDto> SubmitAsync(Guid id);
    Task<PickListDto> CancelAsync(Guid id);
    Task<PickAllocationResultDto> AllocateStockAsync(Guid id);
    Task<List<PendingTransferDto>> GetPendingTransfersAsync(Guid id);
    Task<Guid> CreateDeliveryNoteFromPickListAsync(Guid pickListId);
}
