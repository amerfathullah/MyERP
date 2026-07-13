using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

// === Production Plan DTOs ===

public class ProductionPlanDto : AuditedEntityDto<Guid>
{
    public string PlanNumber { get; set; } = null!;
    public ProductionPlanStatus Status { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public bool CombineItems { get; set; }
    public bool IgnoreExistingOrderedQty { get; set; }
    public bool ConsiderMinimumOrderQty { get; set; }
    public bool IncludeSafetyStock { get; set; }
    public bool SkipAvailableSubAssemblyItem { get; set; }
    public Guid? RawMaterialGroupWarehouseId { get; set; }
    public Guid? ForWarehouseId { get; set; }
    public string? Notes { get; set; }
    public List<ProductionPlanItemDto> PlannedItems { get; set; } = new();
    public List<ProductionPlanMrItemDto> MaterialRequirements { get; set; } = new();
}

public class ProductionPlanItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public Guid BomId { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ProducedQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public Guid? WorkOrderId { get; set; }
}

public class ProductionPlanMrItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal RequiredQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal MinOrderQty { get; set; }
    public decimal SafetyStock { get; set; }
    public string? Uom { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public SubAssemblyType ProcurementType { get; set; }
}

public class CreateProductionPlanDto
{
    [Required] public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; } = DateTime.UtcNow;
    public bool CombineItems { get; set; }
    public bool IgnoreExistingOrderedQty { get; set; }
    public bool ConsiderMinimumOrderQty { get; set; }
    public bool IncludeSafetyStock { get; set; }
    public bool SkipAvailableSubAssemblyItem { get; set; }
    public Guid? RawMaterialGroupWarehouseId { get; set; }
    public Guid? ForWarehouseId { get; set; }
    [StringLength(ProductionPlanConsts.MaxNoteLength)] public string? Notes { get; set; }
    public List<CreateProductionPlanItemDto> Items { get; set; } = new();
}

public class CreateProductionPlanItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required] public string ItemName { get; set; } = null!;
    [Required] public Guid BomId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal PlannedQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? MaterialRequestId { get; set; }
}

public class GetProductionPlanListDto : PagedAndSortedResultRequestDto
{
    public ProductionPlanStatus? Status { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
}

// === Interface ===

public interface IProductionPlanAppService : IApplicationService
{
    Task<ProductionPlanDto> GetAsync(Guid id);
    Task<PagedResultDto<ProductionPlanDto>> GetListAsync(GetProductionPlanListDto input);
    Task<ProductionPlanDto> CreateAsync(CreateProductionPlanDto input);
    Task DeleteAsync(Guid id);
    Task<ProductionPlanDto> SubmitAsync(Guid id);
    Task<ProductionPlanDto> CancelAsync(Guid id);

    /// <summary>Explode BOMs and calculate material requirements for the plan.</summary>
    Task<ProductionPlanDto> CalculateMaterialRequirementsAsync(Guid id);

    /// <summary>Generate Work Orders from planned items.</summary>
    Task<ProductionPlanDto> GenerateWorkOrdersAsync(Guid id);

    /// <summary>Generate Material Requests from material requirements.</summary>
    Task<ProductionPlanDto> GenerateMaterialRequestsAsync(Guid id);
}
