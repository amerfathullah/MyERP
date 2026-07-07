using System;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

public class BranchAppService :
    CrudAppService<
        Branch,
        BranchDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateBranchDto>,
    IBranchAppService
{
    public BranchAppService(IRepository<Branch, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Branches.Default;
        GetListPolicyName = MyERPPermissions.Branches.Default;
        CreatePolicyName = MyERPPermissions.Branches.Create;
        UpdatePolicyName = MyERPPermissions.Branches.Edit;
        DeletePolicyName = MyERPPermissions.Branches.Delete;
    }
}
