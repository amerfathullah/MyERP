using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IAccountingDimensionAppService : IApplicationService
{
    Task<List<AccountingDimensionDto>> GetEnabledDimensionsAsync(Guid? companyId = null);
    Task<PagedResultDto<AccountingDimensionDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AccountingDimensionDto> GetAsync(Guid id);
    Task<AccountingDimensionDto> CreateAsync(CreateAccountingDimensionDto input);
    Task<AccountingDimensionDto> UpdateAsync(Guid id, UpdateAccountingDimensionDto input);
    Task EnableAsync(Guid id);
    Task DisableAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<List<AccountingDimensionFilterDto>> GetFiltersAsync(Guid dimensionId, Guid companyId);
    Task<AccountingDimensionFilterDto> CreateFilterAsync(CreateDimensionFilterDto input);
    Task DeleteFilterAsync(Guid filterId);
}
