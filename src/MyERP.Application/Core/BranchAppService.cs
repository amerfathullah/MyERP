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

    protected override Branch MapToEntity(CreateUpdateBranchDto input)
    {
        var entity = new Branch(
            GuidGenerator.Create(),
            input.CompanyId,
            input.Name,
            CurrentTenant.Id);
        MapUpdateFields(input, entity);
        return entity;
    }

    protected override void MapToEntity(CreateUpdateBranchDto input, Branch entity)
    {
        entity.SetName(input.Name);
        entity.CompanyId = input.CompanyId;
        MapUpdateFields(input, entity);
    }

    private static void MapUpdateFields(CreateUpdateBranchDto input, Branch entity)
    {
        entity.Code = input.Code;
        entity.Phone = input.Phone;
        entity.Email = input.Email;
        entity.Address = input.Address;
        entity.City = input.City;
        entity.State = input.State;
        entity.PostalCode = input.PostalCode;
        entity.Country = input.Country;
        entity.IsActive = input.IsActive;
        entity.IsHeadquarters = input.IsHeadquarters;
    }
}
