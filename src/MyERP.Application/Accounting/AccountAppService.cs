using System;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class AccountAppService :
    CrudAppService<
        Account,
        AccountDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateAccountDto>,
    IAccountAppService
{
    public AccountAppService(IRepository<Account, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Accounts.Default;
        GetListPolicyName = MyERPPermissions.Accounts.Default;
        CreatePolicyName = MyERPPermissions.Accounts.Create;
        UpdatePolicyName = MyERPPermissions.Accounts.Edit;
        DeletePolicyName = MyERPPermissions.Accounts.Delete;
    }
}
