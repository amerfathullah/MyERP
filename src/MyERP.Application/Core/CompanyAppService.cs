using System;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

public class CompanyAppService :
    CrudAppService<
        Company,
        CompanyDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateCompanyDto>,
    ICompanyAppService
{
    public CompanyAppService(IRepository<Company, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Companies.Default;
        GetListPolicyName = MyERPPermissions.Companies.Default;
        CreatePolicyName = MyERPPermissions.Companies.Create;
        UpdatePolicyName = MyERPPermissions.Companies.Edit;
        DeletePolicyName = MyERPPermissions.Companies.Delete;
    }
}
