using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IAccountCategoryAppService : IApplicationService
{
    Task<PagedResultDto<AccountCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<AccountCategoryDto> GetAsync(Guid id);
    Task<AccountCategoryDto> CreateAsync(CreateAccountCategoryDto input);
    Task DeleteAsync(Guid id);
}
