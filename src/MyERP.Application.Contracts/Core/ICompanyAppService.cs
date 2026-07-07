using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface ICompanyAppService :
    ICrudAppService<
        CompanyDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateCompanyDto>
{
}
