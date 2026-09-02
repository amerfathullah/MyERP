using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class GetAccountListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface IAccountAppService :
    ICrudAppService<
        AccountDto,
        Guid,
        GetAccountListDto,
        CreateUpdateAccountDto>
{
    Task<System.Collections.Generic.List<AccountTreeNodeDto>> GetTreeAsync(Guid companyId);
}
