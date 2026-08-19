using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public class CompetitorDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Website { get; set; }
}

public class CreateUpdateCompetitorDto
{
    [Required][StringLength(CompetitorConsts.MaxNameLength)] public string Name { get; set; } = null!;
    [StringLength(CompetitorConsts.MaxWebsiteLength)] public string? Website { get; set; }
}

public class GetCompetitorListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface ICompetitorAppService : IApplicationService
{
    Task<CompetitorDto> GetAsync(Guid id);
    Task<PagedResultDto<CompetitorDto>> GetListAsync(GetCompetitorListDto input);
    Task<CompetitorDto> CreateAsync(CreateUpdateCompetitorDto input);
    Task<CompetitorDto> UpdateAsync(Guid id, CreateUpdateCompetitorDto input);
    Task DeleteAsync(Guid id);
}

public class MarketSegmentDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
}

public class CreateUpdateMarketSegmentDto
{
    [Required][StringLength(MarketSegmentConsts.MaxNameLength)] public string Name { get; set; } = null!;
}

public class GetMarketSegmentListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface IMarketSegmentAppService : IApplicationService
{
    Task<MarketSegmentDto> GetAsync(Guid id);
    Task<PagedResultDto<MarketSegmentDto>> GetListAsync(GetMarketSegmentListDto input);
    Task<MarketSegmentDto> CreateAsync(CreateUpdateMarketSegmentDto input);
    Task<MarketSegmentDto> UpdateAsync(Guid id, CreateUpdateMarketSegmentDto input);
    Task DeleteAsync(Guid id);
}

public class IndustryTypeDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
}

public class CreateUpdateIndustryTypeDto
{
    [Required][StringLength(IndustryTypeConsts.MaxNameLength)] public string Name { get; set; } = null!;
}

public class GetIndustryTypeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface IIndustryTypeAppService : IApplicationService
{
    Task<IndustryTypeDto> GetAsync(Guid id);
    Task<PagedResultDto<IndustryTypeDto>> GetListAsync(GetIndustryTypeListDto input);
    Task<IndustryTypeDto> CreateAsync(CreateUpdateIndustryTypeDto input);
    Task<IndustryTypeDto> UpdateAsync(Guid id, CreateUpdateIndustryTypeDto input);
    Task DeleteAsync(Guid id);
}

public class SalesStageDto : AuditedEntityDto<Guid>
{
    public string StageName { get; set; } = null!;
    public int SortOrder { get; set; }
}

public class CreateUpdateSalesStageDto
{
    [Required][StringLength(SalesStageConsts.MaxStageNameLength)] public string StageName { get; set; } = null!;
    public int SortOrder { get; set; }
}

public class GetSalesStageListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public interface ISalesStageAppService : IApplicationService
{
    Task<SalesStageDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesStageDto>> GetListAsync(GetSalesStageListDto input);
    Task<SalesStageDto> CreateAsync(CreateUpdateSalesStageDto input);
    Task<SalesStageDto> UpdateAsync(Guid id, CreateUpdateSalesStageDto input);
    Task DeleteAsync(Guid id);
}
