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

public class MergeAccountDto
{
    public Guid OldAccountId { get; set; }
    public Guid NewAccountId { get; set; }
}

public interface IAccountAppService :
    ICrudAppService<
        AccountDto,
        Guid,
        GetAccountListDto,
        CreateUpdateAccountDto>
{
    Task<System.Collections.Generic.List<AccountTreeNodeDto>> GetTreeAsync(Guid companyId, bool includeDisabled = false);

    /// <summary>
    /// Merges an account into another account per ERPNext accounts/doctype/account/account.py merge_account.
    /// Validates property compatibility (IsGroup, RootType, Company, Currency, AccountSubType),
    /// circular hierarchy prevention with automatic reparenting, and BalanceMustBe constraints.
    /// Reassigns GL/PLE/rule references and removes the merged account.
    /// </summary>
    Task<AccountDto> MergeAccountAsync(MergeAccountDto input);
}
