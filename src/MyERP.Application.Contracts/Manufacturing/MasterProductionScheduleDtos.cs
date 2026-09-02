using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public class MpsSalesOrderRefDto
{
    public Guid SalesOrderId { get; set; }
    public DateTime SalesOrderDate { get; set; }
    public Guid CustomerId { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Status { get; set; }
}

public class MpsMaterialRequestRefDto
{
    public Guid MaterialRequestId { get; set; }
    public DateTime MaterialRequestDate { get; set; }
}

public class MasterProductionScheduleItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public Guid? BomId { get; set; }
    public string Uom { get; set; } = "Unit";
    public Guid? WarehouseId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public decimal PlannedQty { get; set; }
    public int CumulativeLeadTimeDays { get; set; }
    public DateTime OrderReleaseDate { get; set; }
}

public class MasterProductionScheduleDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ScheduleNumber { get; set; } = null!;
    public int Status { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ParentWarehouseId { get; set; }
    public Guid? SalesForecastId { get; set; }
    public List<MpsSalesOrderRefDto> SalesOrders { get; set; } = new();
    public List<MpsMaterialRequestRefDto> MaterialRequests { get; set; } = new();
    public List<MasterProductionScheduleItemDto> Items { get; set; } = new();
}

public class CreateMasterProductionScheduleDto
{
    public Guid CompanyId { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime FromDate { get; set; }
    public Guid? ParentWarehouseId { get; set; }
    public Guid? SalesForecastId { get; set; }
}

public class UpdateMasterProductionScheduleDto
{
    public DateTime PostingDate { get; set; }
    public DateTime FromDate { get; set; }
    public Guid? ParentWarehouseId { get; set; }
    public Guid? SalesForecastId { get; set; }

    /// <summary>Planned qty edits on already-computed rows (id must match an existing item).</summary>
    public List<MasterProductionScheduleItemDto> Items { get; set; } = new();
}

public class FetchSalesOrdersDto
{
    public Guid? CustomerId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class FetchMaterialRequestsDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class CreateProductionPlanFromMpsInput
{
    public Guid? ForWarehouseId { get; set; }
    public bool CombineItems { get; set; }
}

public class MasterProductionScheduleSummaryDto
{
    public Guid Id { get; set; }
    public string ScheduleNumber { get; set; } = null!;
    public int Status { get; set; }
    public int TotalItemsCount { get; set; }
    public decimal TotalPlannedQty { get; set; }
    public DateTime? EarliestReleaseDate { get; set; }
    public DateTime? LatestDeliveryDate { get; set; }
    public int SalesOrdersCount { get; set; }
    public int MaterialRequestsCount { get; set; }
}

public interface IMasterProductionScheduleAppService : IApplicationService
{
    Task<MasterProductionScheduleDto> GetAsync(Guid id);
    Task<PagedResultDto<MasterProductionScheduleDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<MasterProductionScheduleDto> CreateAsync(CreateMasterProductionScheduleDto input);
    Task<MasterProductionScheduleDto> UpdateAsync(Guid id, UpdateMasterProductionScheduleDto input);
    Task DeleteAsync(Guid id);
    Task<MasterProductionScheduleDto> FetchSalesOrdersAsync(Guid id, FetchSalesOrdersDto input);
    Task<MasterProductionScheduleDto> FetchMaterialRequestsAsync(Guid id, FetchMaterialRequestsDto input);
    Task<MasterProductionScheduleDto> ComputeActualDemandAsync(Guid id);
    Task<MasterProductionScheduleDto> SubmitAsync(Guid id);
    Task<MasterProductionScheduleDto> CancelAsync(Guid id);
    Task<Guid> MakeProductionPlanAsync(Guid id, CreateProductionPlanFromMpsInput? input = null);
    Task<MasterProductionScheduleSummaryDto> GetSummaryAsync(Guid id);
}
