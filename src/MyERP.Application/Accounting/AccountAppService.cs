using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

using MyERP;

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

    /// <summary>
    /// Prevent deletion of accounts with GL entries (Journal Entry lines).
    /// Per DO-NOT: cannot delete accounts that have been used in transactions.
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JournalEntry, Guid>>();
        var jeQuery = await jeRepo.GetQueryableAsync();
        var hasGLEntries = jeQuery.Any(je =>
            je.Lines.Any(l => l.AccountId == id));

        if (hasGLEntries)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AccountCannotBeDeleted)
                .WithData("reason", "Account has been used in Journal Entries. Deactivate instead of deleting.");
        }

        // Check if account is a group (has children)
        var account = await Repository.GetAsync(id);
        if (account.IsGroup)
        {
            var childQuery = await Repository.GetQueryableAsync();
            var hasChildren = childQuery.Any(a => a.ParentAccountId == id);
            if (hasChildren)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AccountCannotBeDeleted)
                    .WithData("reason", "Group account has child accounts. Delete children first.");
            }
        }

        await base.DeleteAsync(id);
    }
}
