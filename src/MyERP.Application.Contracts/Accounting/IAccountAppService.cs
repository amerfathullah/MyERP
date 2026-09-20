using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class GetAccountListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
    public AccountSubType? AccountSubType { get; set; }
    public bool? IsGroup { get; set; }
}

public interface IAccountAppService :
    ICrudAppService<
        AccountDto,
        Guid,
        GetAccountListDto,
        CreateUpdateAccountDto>
{
    Task<System.Collections.Generic.List<AccountTreeNodeDto>> GetTreeAsync(Guid companyId, bool includeDisabled = false);
}
