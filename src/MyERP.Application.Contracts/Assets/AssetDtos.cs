using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using System.Threading.Tasks;

namespace MyERP.Assets;

// === DTOs ===

public class AssetDto : AuditedEntityDto<Guid>
{
    public string AssetNumber { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public AssetStatus Status { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public string? Location { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal TotalAssetCost { get; set; }
    public bool CalculateDepreciation { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal ValueAfterDepreciation { get; set; }
    public bool IsFullyDepreciated { get; set; }
    public DateTime? DisposalDate { get; set; }
    public decimal? DisposalAmount { get; set; }
    public string? Notes { get; set; }
    public List<DepreciationScheduleDto> Schedule { get; set; } = new();
}

public class DepreciationScheduleDto
{
    public Guid Id { get; set; }
    public DateTime ScheduleDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public bool IsBooked { get; set; }
}

public class CreateAssetDto
{
    [Required]
    [StringLength(AssetConsts.MaxAssetNameLength)]
    public string AssetName { get; set; } = null!;

    [Required]
    public Guid CompanyId { get; set; }

    public Guid? AssetCategoryId { get; set; }

    [StringLength(AssetConsts.MaxLocationLength)]
    public string? Location { get; set; }

    [Required]
    public DateTime PurchaseDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PurchaseAmount { get; set; }

    public decimal AdditionalCost { get; set; }
    public bool CalculateDepreciation { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal DepreciationRate { get; set; }
    public int FrequencyMonths { get; set; } = 12;
    public DateTime? AvailableForUseDate { get; set; }

    [StringLength(AssetConsts.MaxNoteLength)]
    public string? Notes { get; set; }
}

public class UpdateAssetDto
{
    [Required]
    [StringLength(AssetConsts.MaxAssetNameLength)]
    public string AssetName { get; set; } = null!;

    public Guid? AssetCategoryId { get; set; }

    [StringLength(AssetConsts.MaxLocationLength)]
    public string? Location { get; set; }

    public decimal AdditionalCost { get; set; }

    [StringLength(AssetConsts.MaxNoteLength)]
    public string? Notes { get; set; }
}

public class GetAssetListDto : PagedAndSortedResultRequestDto
{
    public AssetStatus? Status { get; set; }
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? AssetCategoryId { get; set; }
}

public class AssetCategoryDto
{
    public Guid Id { get; set; }
    public string CategoryName { get; set; } = null!;
    public bool IsDepreciable { get; set; }
    public DepreciationMethod DefaultDepreciationMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; }
}

public class CreateUpdateAssetCategoryDto
{
    [Required]
    [StringLength(AssetCategoryConsts.MaxCategoryNameLength)]
    public string CategoryName { get; set; } = null!;

    public bool IsDepreciable { get; set; } = true;
    public DepreciationMethod DefaultDepreciationMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; } = 60;
    public decimal? DefaultDepreciationRate { get; set; }
}

// === Interface ===

public interface IAssetAppService : IApplicationService
{
    Task<AssetDto> GetAsync(Guid id);
    Task<PagedResultDto<AssetDto>> GetListAsync(GetAssetListDto input);
    Task<AssetDto> CreateAsync(CreateAssetDto input);
    Task<AssetDto> UpdateAsync(Guid id, UpdateAssetDto input);
    Task DeleteAsync(Guid id);
    Task<AssetDto> SubmitAsync(Guid id);
    Task<AssetDto> SellAsync(Guid id, DateTime disposalDate, decimal amount);
    Task<AssetDto> ScrapAsync(Guid id, DateTime disposalDate);
    Task<AssetCategoryDto[]> GetCategoriesAsync();
    Task<AssetCategoryDto> CreateCategoryAsync(CreateUpdateAssetCategoryDto input);
}
