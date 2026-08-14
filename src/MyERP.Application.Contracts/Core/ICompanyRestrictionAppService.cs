using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface ICompanyRestrictionAppService : IApplicationService
{
    Task<CompanyRestrictionDto> GetAsync(string parentType, Guid parentId);
    Task SaveAsync(SaveCompanyRestrictionDto input);
}
