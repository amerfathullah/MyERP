using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public class ContractTemplateFulfilmentTermDto
{
    public Guid Id { get; set; }
    public string TermText { get; set; } = null!;
}

public class ContractTemplateDto : AuditedEntityDto<Guid>
{
    public string Title { get; set; } = null!;
    public string? ContractTerms { get; set; }
    public bool RequiresFulfilment { get; set; }
    public List<ContractTemplateFulfilmentTermDto> FulfilmentTerms { get; set; } = new();
}

public class CreateFulfilmentTermDto
{
    [Required][StringLength(ContractTemplateConsts.MaxFulfilmentTermLength)] public string TermText { get; set; } = null!;
}

public class CreateUpdateContractTemplateDto
{
    [Required][StringLength(ContractTemplateConsts.MaxTitleLength)] public string Title { get; set; } = null!;
    public string? ContractTerms { get; set; }
    public bool RequiresFulfilment { get; set; }
    public List<CreateFulfilmentTermDto> FulfilmentTerms { get; set; } = new();
}

public class GetContractTemplateListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface IContractTemplateAppService : IApplicationService
{
    Task<ContractTemplateDto> GetAsync(Guid id);
    Task<PagedResultDto<ContractTemplateDto>> GetListAsync(GetContractTemplateListDto input);
    Task<ContractTemplateDto> CreateAsync(CreateUpdateContractTemplateDto input);
    Task<ContractTemplateDto> UpdateAsync(Guid id, CreateUpdateContractTemplateDto input);
    Task DeleteAsync(Guid id);
}
