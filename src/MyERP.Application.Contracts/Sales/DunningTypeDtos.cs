using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public class DunningLetterTextDto
{
    public Guid Id { get; set; }
    public string? Language { get; set; }
    public bool IsDefaultLanguage { get; set; }
    public string? BodyText { get; set; }
    public string? ClosingText { get; set; }
}

public class DunningTypeDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string DunningTypeName { get; set; } = null!;
    public bool IsDefault { get; set; }
    public decimal DunningFee { get; set; }
    public decimal RateOfInterest { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public List<DunningLetterTextDto> LetterText { get; set; } = new();
}

public class CreateDunningLetterTextDto
{
    public string? Language { get; set; }
    public bool IsDefaultLanguage { get; set; }
    public string? BodyText { get; set; }
    public string? ClosingText { get; set; }
}

public class CreateDunningTypeDto
{
    public Guid CompanyId { get; set; }
    public string DunningTypeName { get; set; } = null!;
    public bool IsDefault { get; set; }
    public decimal DunningFee { get; set; }
    public decimal RateOfInterest { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public List<CreateDunningLetterTextDto> LetterText { get; set; } = new();
}

public class UpdateDunningTypeDto : CreateDunningTypeDto
{
}

public interface IDunningTypeAppService : IApplicationService
{
    Task<PagedResultDto<DunningTypeDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<DunningTypeDto> GetAsync(Guid id);
    Task<DunningTypeDto> CreateAsync(CreateDunningTypeDto input);
    Task<DunningTypeDto> UpdateAsync(Guid id, UpdateDunningTypeDto input);
    Task DeleteAsync(Guid id);
}
