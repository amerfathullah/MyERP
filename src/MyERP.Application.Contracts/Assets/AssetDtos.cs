using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyERP.Core;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

// === Asset DTOs ===

public class AssetDto : FullAuditedEntityDto<Guid>
{
    public string AssetNumber { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public AssetStatus Status { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public string? AssetCategoryName { get; set; }
    public Guid? ItemId { get; set; }
    public string? Location { get; set; }
    public Guid? CustodianEmployeeId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal TotalAssetCost { get; set; }
    public Guid? PurchaseReceiptId { get; set; }
    public Guid? PurchaseInvoiceId { get; set; }
    public bool CalculateDepreciation { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal DepreciationRate { get; set; }
    public int FrequencyMonths { get; set; } = 12;
    public DateTime? AvailableForUseDate { get; set; }
    public decimal OpeningAccumulatedDepreciation { get; set; }
    public decimal ValueAfterDepreciation { get; set; }
    public bool IsFullyDepreciated { get; set; }
    public DateTime? DisposalDate { get; set; }
    public decimal? DisposalAmount { get; set; }
    public string? Notes { get; set; }
    public List<DepreciationScheduleDto> Schedule { get; set; } = new();
}

public class DepreciationScheduleDto : EntityDto<Guid>
{
    public DateTime ScheduleDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public bool IsBooked { get; set; }
    public Guid? ShiftFactorId { get; set; }
}

public class CreateAssetDto
{
    [Required]
    [StringLength(AssetConsts.MaxAssetNameLength)]
    public string AssetName { get; set; } = null!;

    [Required]
    public Guid CompanyId { get; set; }

    public Guid? AssetCategoryId { get; set; }
    public Guid? ItemId { get; set; }

    [StringLength(AssetConsts.MaxLocationLength)]
    public string? Location { get; set; }

    public Guid? CustodianEmployeeId { get; set; }

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
    public decimal OpeningAccumulatedDepreciation { get; set; }

    [StringLength(AssetConsts.MaxNoteLength)]
    public string? Notes { get; set; }
}

public class UpdateAssetDto
{
    [Required]
    [StringLength(AssetConsts.MaxAssetNameLength)]
    public string AssetName { get; set; } = null!;

    public Guid? AssetCategoryId { get; set; }
    public Guid? ItemId { get; set; }

    [StringLength(AssetConsts.MaxLocationLength)]
    public string? Location { get; set; }

    public Guid? CustodianEmployeeId { get; set; }
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

public class GetAssetListDto : PagedAndSortedResultRequestDto
{
    public AssetStatus? Status { get; set; }
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

// === Asset Category DTOs ===

public class AssetCategoryAccountDto : FullAuditedEntityDto<Guid>
{
    public Guid AssetCategoryId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FixedAssetAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
    public Guid? DepreciationExpenseAccountId { get; set; }
    public Guid? CapitalWorkInProgressAccountId { get; set; }
}

public class CreateUpdateAssetCategoryAccountDto
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FixedAssetAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
    public Guid? DepreciationExpenseAccountId { get; set; }
    public Guid? CapitalWorkInProgressAccountId { get; set; }
}

public class AssetCategoryDto : FullAuditedEntityDto<Guid>
{
    public string CategoryName { get; set; } = null!;
    public bool IsDepreciable { get; set; }
    public bool EnableCwipAccounting { get; set; }
    public bool NonDepreciableCategory { get; set; }
    public DepreciationMethod DefaultDepreciationMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; }
    public decimal? DefaultDepreciationRate { get; set; }
    public int DefaultFrequencyMonths { get; set; }
    public Guid? AssetAccountId { get; set; }
    public Guid? DepreciationAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
    public List<AssetCategoryAccountDto> Accounts { get; set; } = new();
}

public class CreateUpdateAssetCategoryDto
{
    [Required]
    [StringLength(AssetCategoryConsts.MaxCategoryNameLength)]
    public string CategoryName { get; set; } = null!;

    public bool IsDepreciable { get; set; } = true;
    public bool EnableCwipAccounting { get; set; }
    public bool NonDepreciableCategory { get; set; }
    public DepreciationMethod DefaultDepreciationMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; } = 60;
    public decimal? DefaultDepreciationRate { get; set; }
    public int DefaultFrequencyMonths { get; set; } = 12;
    public Guid? AssetAccountId { get; set; }
    public Guid? DepreciationAccountId { get; set; }
    public Guid? AccumulatedDepreciationAccountId { get; set; }
    public List<CreateUpdateAssetCategoryAccountDto> Accounts { get; set; } = new();
}

// === Asset Movement DTOs ===

public class AssetMovementItemDto : FullAuditedEntityDto<Guid>
{
    public Guid AssetMovementId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public string? SourceLocation { get; set; }
    public string? TargetLocation { get; set; }
    public Guid? FromEmployeeId { get; set; }
    public Guid? ToEmployeeId { get; set; }
}

public class CreateUpdateAssetMovementItemDto
{
    public Guid? Id { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public string? SourceLocation { get; set; }
    public string? TargetLocation { get; set; }
    public Guid? FromEmployeeId { get; set; }
    public Guid? ToEmployeeId { get; set; }
}

public class AssetMovementDto : FullAuditedEntityDto<Guid>
{
    public string MovementNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public AssetMovementPurpose Purpose { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public Guid? AssetId { get; set; }
    public string? SourceLocation { get; set; }
    public Guid? SourceEmployeeId { get; set; }
    public string? TargetLocation { get; set; }
    public Guid? TargetEmployeeId { get; set; }
    public DocumentStatus Status { get; set; }
    public List<AssetMovementItemDto> Items { get; set; } = new();
}

public class CreateUpdateAssetMovementDto
{
    public Guid CompanyId { get; set; }
    public AssetMovementPurpose Purpose { get; set; } = AssetMovementPurpose.Transfer;
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public Guid? AssetId { get; set; }
    public string? SourceLocation { get; set; }
    public Guid? SourceEmployeeId { get; set; }
    public string? TargetLocation { get; set; }
    public Guid? TargetEmployeeId { get; set; }
    public List<CreateUpdateAssetMovementItemDto> Items { get; set; } = new();
}

// === Asset Repair DTOs ===

public class AssetRepairConsumedItemDto : FullAuditedEntityDto<Guid>
{
    public Guid AssetRepairId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal TotalValue { get; set; }
    public string? SerialAndBatchBundleId { get; set; }
}

public class CreateUpdateAssetRepairConsumedItemDto
{
    public Guid? Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public decimal ValuationRate { get; set; }
    public string? SerialAndBatchBundleId { get; set; }
}

public class AssetRepairPurchaseInvoiceDto : FullAuditedEntityDto<Guid>
{
    public Guid AssetRepairId { get; set; }
    public Guid PurchaseInvoiceId { get; set; }
    public string? PurchaseInvoiceNumber { get; set; }
    public decimal RepairCost { get; set; }
    public Guid? ExpenseAccountId { get; set; }
}

public class CreateUpdateAssetRepairPurchaseInvoiceDto
{
    public Guid? Id { get; set; }
    public Guid PurchaseInvoiceId { get; set; }
    public string? PurchaseInvoiceNumber { get; set; }
    public decimal RepairCost { get; set; }
    public Guid? ExpenseAccountId { get; set; }
}

public class AssetRepairDto : FullAuditedEntityDto<Guid>
{
    public string RepairNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public string? RepairDescription { get; set; }
    public string? ActionsPerformed { get; set; }
    public string? Downtime { get; set; }
    public DateTime? FailureDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal RepairCost { get; set; }
    public decimal ConsumedItemsCost { get; set; }
    public decimal TotalRepairCost { get; set; }
    public bool CapitalizeRepairCost { get; set; }
    public int IncreaseInAssetLife { get; set; }
    public AssetRepairStatus Status { get; set; }
    public List<AssetRepairConsumedItemDto> StockItems { get; set; } = new();
    public List<AssetRepairPurchaseInvoiceDto> Invoices { get; set; } = new();
}

public class CreateUpdateAssetRepairDto
{
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string? RepairDescription { get; set; }
    public string? ActionsPerformed { get; set; }
    public string? Downtime { get; set; }
    public DateTime? FailureDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal RepairCost { get; set; }
    public bool CapitalizeRepairCost { get; set; }
    public int IncreaseInAssetLife { get; set; }
    public List<CreateUpdateAssetRepairConsumedItemDto> StockItems { get; set; } = new();
    public List<CreateUpdateAssetRepairPurchaseInvoiceDto> Invoices { get; set; } = new();
}

// === Asset Capitalization DTOs ===

public class AssetCapitalizationStockItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class CreateUpdateAssetCapitalizationStockItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class AssetCapitalizationServiceItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Amount { get; set; }
    public Guid? ExpenseAccountId { get; set; }
}

public class CreateUpdateAssetCapitalizationServiceItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Amount { get; set; }
    public Guid? ExpenseAccountId { get; set; }
}

public class AssetCapitalizationAssetItemDto : EntityDto<Guid>
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = null!;
    public decimal CurrentValue { get; set; }
}

public class CreateUpdateAssetCapitalizationAssetItemDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = null!;
    public decimal CurrentValue { get; set; }
}

public class AssetCapitalizationDto : FullAuditedEntityDto<Guid>
{
    public string CapitalizationNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public Guid TargetAssetId { get; set; }
    public string? TargetAssetName { get; set; }
    public decimal TotalCapitalizedAmount { get; set; }
    public AssetCapitalizationStatus Status { get; set; }
    public List<AssetCapitalizationStockItemDto> StockItems { get; set; } = new();
    public List<AssetCapitalizationServiceItemDto> ServiceItems { get; set; } = new();
    public List<AssetCapitalizationAssetItemDto> ConsumedAssets { get; set; } = new();
}

public class CreateUpdateAssetCapitalizationDto
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; } = DateTime.UtcNow;
    public Guid TargetAssetId { get; set; }
    public string? TargetAssetName { get; set; }
    public List<CreateUpdateAssetCapitalizationStockItemDto> StockItems { get; set; } = new();
    public List<CreateUpdateAssetCapitalizationServiceItemDto> ServiceItems { get; set; } = new();
    public List<CreateUpdateAssetCapitalizationAssetItemDto> ConsumedAssets { get; set; } = new();
}

// === Asset Value Adjustment DTOs ===

public class AssetValueAdjustmentDto : FullAuditedEntityDto<Guid>
{
    public string AdjustmentNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string? AssetName { get; set; }
    public Guid? FinanceBookId { get; set; }
    public DateTime Date { get; set; }
    public decimal CurrentAssetValue { get; set; }
    public decimal NewAssetValue { get; set; }
    public decimal DifferenceAmount { get; set; }
    public Guid DifferenceAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public DocumentStatus Status { get; set; }
}

public class CreateUpdateAssetValueAdjustmentDto
{
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public Guid? FinanceBookId { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public decimal CurrentAssetValue { get; set; }
    public decimal NewAssetValue { get; set; }
    public Guid DifferenceAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? Notes { get; set; }
}

// === Asset Activity DTOs ===

public class AssetActivityDto : FullAuditedEntityDto<Guid>
{
    public Guid AssetId { get; set; }
    public AssetActivityType ActivityType { get; set; }
    public string Subject { get; set; } = null!;
    public string? Details { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
}

public class CreateAssetActivityDto
{
    public Guid AssetId { get; set; }
    public AssetActivityType ActivityType { get; set; }
    [Required]
    [StringLength(AssetConsts.MaxSubjectLength)]
    public string Subject { get; set; } = null!;
    public string? Details { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
}

// === Locations ===

public class LocationDto : FullAuditedEntityDto<Guid>
{
    public string LocationName { get; set; } = null!;
    public Guid? ParentLocationId { get; set; }
    public string? ParentLocationName { get; set; }
    public bool IsContainer { get; set; }
    public bool IsGroup { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class CreateUpdateLocationDto
{
    [Required]
    [StringLength(LocationConsts.MaxLocationNameLength)]
    public string LocationName { get; set; } = null!;

    public Guid? ParentLocationId { get; set; }
    public bool IsContainer { get; set; }
    public bool IsGroup { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public interface ILocationAppService : IApplicationService
{
    System.Threading.Tasks.Task<LocationDto> GetAsync(Guid id);
    System.Threading.Tasks.Task<PagedResultDto<LocationDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    System.Threading.Tasks.Task<LocationDto> CreateAsync(CreateUpdateLocationDto input);
    System.Threading.Tasks.Task<LocationDto> UpdateAsync(Guid id, CreateUpdateLocationDto input);
    System.Threading.Tasks.Task DeleteAsync(Guid id);
}

// === Asset Shift Factors ===

public class AssetShiftFactorDto : FullAuditedEntityDto<Guid>
{
    public string ShiftName { get; set; } = null!;
    public decimal Factor { get; set; }
    public bool IsDefault { get; set; }
}

public class CreateUpdateAssetShiftFactorDto
{
    [Required]
    [StringLength(AssetShiftFactorConsts.MaxShiftNameLength)]
    public string ShiftName { get; set; } = null!;

    public decimal Factor { get; set; } = 1;
    public bool IsDefault { get; set; }
}

public interface IAssetShiftFactorAppService : IApplicationService
{
    System.Threading.Tasks.Task<AssetShiftFactorDto> GetAsync(Guid id);
    System.Threading.Tasks.Task<PagedResultDto<AssetShiftFactorDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    System.Threading.Tasks.Task<AssetShiftFactorDto> CreateAsync(CreateUpdateAssetShiftFactorDto input);
    System.Threading.Tasks.Task<AssetShiftFactorDto> UpdateAsync(Guid id, CreateUpdateAssetShiftFactorDto input);
    System.Threading.Tasks.Task DeleteAsync(Guid id);
}

// === Asset Shift Allocation ===

public class AssetShiftAllocationLineDto : EntityDto<Guid>
{
    public Guid ScheduleEntryId { get; set; }
    public Guid ShiftFactorId { get; set; }
    public string? ShiftFactorName { get; set; }
    public DateTime ScheduleDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
}

public class AssetShiftAllocationDto : FullAuditedEntityDto<Guid>
{
    public string AllocationNumber { get; set; } = null!;
    public Guid AssetId { get; set; }
    public Guid? FinanceBookId { get; set; }
    public DocumentStatus Status { get; set; }
    public List<AssetShiftAllocationLineDto> Lines { get; set; } = new();
}

public class AssignShiftLineDto
{
    [Required] public Guid ScheduleEntryId { get; set; }
    [Required] public Guid ShiftFactorId { get; set; }
}

public class CreateAssetShiftAllocationDto
{
    [Required] public Guid AssetId { get; set; }
    public Guid? FinanceBookId { get; set; }
    [Required][MinLength(1)] public List<AssignShiftLineDto> Lines { get; set; } = new();
}

public interface IAssetShiftAllocationAppService : IApplicationService
{
    System.Threading.Tasks.Task<AssetShiftAllocationDto> GetAsync(Guid id);
    System.Threading.Tasks.Task<PagedResultDto<AssetShiftAllocationDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    System.Threading.Tasks.Task<PagedResultDto<DepreciationScheduleDto>> GetUnbookedScheduleAsync(Guid assetId, Guid? financeBookId);
    System.Threading.Tasks.Task<AssetShiftAllocationDto> CreateAsync(CreateAssetShiftAllocationDto input);
    System.Threading.Tasks.Task<AssetShiftAllocationDto> SubmitAsync(Guid id);
    System.Threading.Tasks.Task<AssetShiftAllocationDto> CancelAsync(Guid id);
}

// === Interfaces ===

public interface IAssetAppService : IApplicationService
{
    System.Threading.Tasks.Task<AssetDto> GetAsync(Guid id);
    System.Threading.Tasks.Task<PagedResultDto<AssetDto>> GetListAsync(GetAssetListDto input);
    System.Threading.Tasks.Task<AssetDto> CreateAsync(CreateAssetDto input);
    System.Threading.Tasks.Task<AssetDto> UpdateAsync(Guid id, UpdateAssetDto input);
    System.Threading.Tasks.Task DeleteAsync(Guid id);
    System.Threading.Tasks.Task<AssetDto> SubmitAsync(Guid id);
    System.Threading.Tasks.Task<AssetDto> SellAsync(Guid id, DateTime disposalDate, decimal amount);
    System.Threading.Tasks.Task<AssetDto> ScrapAsync(Guid id, DateTime disposalDate);
    System.Threading.Tasks.Task<AssetCategoryDto[]> GetCategoriesAsync();
    System.Threading.Tasks.Task<AssetCategoryDto> CreateCategoryAsync(CreateUpdateAssetCategoryDto input);
}
