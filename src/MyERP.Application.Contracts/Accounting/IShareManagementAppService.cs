using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IShareTypeAppService : IApplicationService
{
    Task<ListResultDto<ShareTypeDto>> GetListAsync();
    Task<ShareTypeDto> CreateAsync(CreateUpdateShareTypeDto input);
    Task<ShareTypeDto> UpdateAsync(Guid id, CreateUpdateShareTypeDto input);
    Task DeleteAsync(Guid id);
}

public interface IShareholderAppService : IApplicationService
{
    Task<PagedResultDto<ShareholderDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<ShareholderDto> GetAsync(Guid id);
    Task<ShareholderDto> CreateAsync(CreateUpdateShareholderDto input);
    Task<ShareholderDto> UpdateAsync(Guid id, CreateUpdateShareholderDto input);
    Task DeleteAsync(Guid id);
}

public interface IShareTransferAppService : IApplicationService
{
    Task<PagedResultDto<ShareTransferDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<ShareTransferDto> GetAsync(Guid id);
    Task<ShareTransferDto> CreateAsync(CreateUpdateShareTransferDto input);
    Task<ShareTransferDto> UpdateAsync(Guid id, CreateUpdateShareTransferDto input);
    Task DeleteAsync(Guid id);
    Task<ShareTransferDto> SubmitAsync(Guid id);
    Task<ShareTransferDto> CancelAsync(Guid id);
}
