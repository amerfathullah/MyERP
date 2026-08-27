using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Manufacturing;
using MyERP.Shared;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace MyERP.Controllers;

[Route("api/app/manufacturing")]
public class ManufacturingController : MyERPController
{
    private readonly IManufacturingAppService _service;
    private readonly IWorkstationAppService _workstationService;
    private readonly IOperationAppService _operationService;
    private readonly IRoutingAppService _routingService;

    public ManufacturingController(
        IManufacturingAppService service,
        IWorkstationAppService workstationService,
        IOperationAppService operationService,
        IRoutingAppService routingService)
    {
        _service = service;
        _workstationService = workstationService;
        _operationService = operationService;
        _routingService = routingService;
    }

    // Operations
    [HttpGet("operations/{id}")]
    public Task<OperationDto> GetOperationAsync(Guid id) => _operationService.GetAsync(id);

    [HttpGet("operations")]
    public Task<PagedResultDto<OperationDto>> GetOperationListAsync([FromQuery] PagedAndSortedResultRequestDto input) => _operationService.GetListAsync(input);

    [HttpPost("operations")]
    public Task<OperationDto> CreateOperationAsync([FromBody] CreateOperationDto input) => _operationService.CreateAsync(input);

    [HttpPut("operations/{id}")]
    public Task<OperationDto> UpdateOperationAsync(Guid id, [FromBody] CreateOperationDto input) => _operationService.UpdateAsync(id, input);

    [HttpDelete("operations/{id}")]
    public Task DeleteOperationAsync(Guid id) => _operationService.DeleteAsync(id);

    // Routings
    [HttpGet("routings/{id}")]
    public Task<RoutingDto> GetRoutingAsync(Guid id) => _routingService.GetAsync(id);

    [HttpGet("routings")]
    public Task<PagedResultDto<RoutingDto>> GetRoutingListAsync([FromQuery] PagedAndSortedResultRequestDto input) => _routingService.GetListAsync(input);

    [HttpPost("routings")]
    public Task<RoutingDto> CreateRoutingAsync([FromBody] CreateRoutingDto input) => _routingService.CreateAsync(input);

    [HttpPut("routings/{id}")]
    public Task<RoutingDto> UpdateRoutingAsync(Guid id, [FromBody] CreateRoutingDto input) => _routingService.UpdateAsync(id, input);

    [HttpDelete("routings/{id}")]
    public Task DeleteRoutingAsync(Guid id) => _routingService.DeleteAsync(id);

    // Workstations
    [HttpGet("workstations/{id}")]
    public Task<WorkstationDto> GetWorkstationAsync(Guid id) => _workstationService.GetAsync(id);

    [HttpGet("workstations")]
    public Task<PagedResultDto<WorkstationDto>> GetWorkstationListAsync([FromQuery] CompanyFilteredPagedRequestDto input) => _workstationService.GetListAsync(input);

    [HttpPut("workstations/{id}")]
    public Task<WorkstationDto> UpdateWorkstationAsync(Guid id, [FromBody] CreateWorkstationDto input) => _workstationService.UpdateAsync(id, input);

    [HttpPost("workstations")]
    public Task<WorkstationDto> CreateWorkstationAsync([FromBody] CreateWorkstationDto input) => _workstationService.CreateAsync(input);

    [HttpGet("workstations/capacity-utilization")]
    public Task<System.Collections.Generic.List<WorkstationUtilizationDto>> GetCapacityUtilizationAsync([FromQuery] Guid? companyId) => _workstationService.GetCapacityUtilizationAsync(companyId);

    // BOM
    [HttpGet("bom/{id}")]
    public Task<BomDto> GetBomAsync(Guid id) => _service.GetBomAsync(id);

    [HttpGet("bom")]
    public Task<PagedResultDto<BomDto>> GetBomListAsync([FromQuery] MyERP.Shared.CompanyFilteredPagedRequestDto input) => _service.GetBomListAsync(input);

    [HttpPost("bom")]
    public Task<BomDto> CreateBomAsync([FromBody] CreateBomDto input) => _service.CreateBomAsync(input);

    [HttpPut("bom/{id}")]
    public Task<BomDto> UpdateBomAsync(Guid id, [FromBody] CreateBomDto input) => _service.UpdateBomAsync(id, input);

    [HttpDelete("bom/{id}")]
    public Task DeleteBomAsync(Guid id) => _service.DeleteBomAsync(id);

    [HttpPost("bom/{id}/update-cost")]
    public Task<BomDto> UpdateBomCostAsync(Guid id) => _service.UpdateBomCostAsync(id);

    // Work Order
    [HttpGet("work-order/{id}")]
    public Task<WorkOrderDto> GetWorkOrderAsync(Guid id) => _service.GetWorkOrderAsync(id);

    [HttpGet("work-order")]
    public Task<PagedResultDto<WorkOrderDto>> GetWorkOrderListAsync([FromQuery] GetWorkOrderListDto input) => _service.GetWorkOrderListAsync(input);

    [HttpPost("work-order")]
    public Task<WorkOrderDto> CreateWorkOrderAsync([FromBody] CreateWorkOrderDto input) => _service.CreateWorkOrderAsync(input);

    [HttpDelete("work-order/{id}")]
    public Task DeleteWorkOrderAsync(Guid id) => _service.DeleteWorkOrderAsync(id);

    [HttpPost("work-order/{id}/submit")]
    public Task<WorkOrderDto> SubmitWorkOrderAsync(Guid id) => _service.SubmitWorkOrderAsync(id);

    [HttpPost("work-order/{id}/start")]
    public Task<WorkOrderDto> StartWorkOrderAsync(Guid id) => _service.StartWorkOrderAsync(id);

    [HttpPost("work-order/{id}/record-production")]
    public Task<WorkOrderDto> RecordProductionAsync(Guid id, [FromQuery] decimal quantity, [FromQuery] decimal processLossQty = 0) => _service.RecordProductionAsync(id, quantity, processLossQty);

    [HttpPost("work-order/{id}/stop")]
    public Task<WorkOrderDto> StopWorkOrderAsync(Guid id) => _service.StopWorkOrderAsync(id);

    [HttpPost("work-order/{id}/unstop")]
    public Task<WorkOrderDto> UnstopWorkOrderAsync(Guid id) => _service.UnstopWorkOrderAsync(id);

    [HttpPost("work-order/{id}/cancel")]
    public Task<WorkOrderDto> CancelWorkOrderAsync(Guid id) => _service.CancelWorkOrderAsync(id);

    [HttpPost("work-order/material-consumption")]
    public Task<MaterialConsumptionResultDto> CreateMaterialConsumptionAsync([FromBody] CreateMaterialConsumptionDto input) => _service.CreateMaterialConsumptionAsync(input);

    // BOM Subcontracting Lookup
    [HttpGet("bom/subcontracting-items")]
    public Task<SubcontractingBomItemsDto> GetBomItemsForSubcontractingAsync([FromQuery] Guid itemId, [FromQuery] Guid companyId, [FromQuery] decimal fgQty = 1)
        => _service.GetBomItemsForSubcontractingAsync(itemId, companyId, fgQty);

    // Job Cards for Work Order (operations progress)
    [HttpGet("work-order/{workOrderId}/job-cards")]
    public Task<PagedResultDto<WorkOrderJobCardDto>> GetWorkOrderJobCardsAsync(Guid workOrderId)
        => _service.GetWorkOrderJobCardsAsync(workOrderId);

    [HttpPost("work-order/{workOrderId}/create-job-cards")]
    public Task<List<WorkOrderJobCardDto>> CreateJobCardsForWorkOrderAsync(Guid workOrderId)
        => _service.CreateJobCardsForWorkOrderAsync(workOrderId);

    [HttpGet("work-order/{workOrderId}/cost-breakdown")]
    public Task<ProductionCostBreakdownDto> GetProductionCostBreakdownAsync(Guid workOrderId)
        => _service.GetProductionCostBreakdownAsync(workOrderId);

    [HttpGet("production-schedule")]
    public Task<ProductionScheduleDto> GetProductionScheduleAsync([FromQuery] Guid companyId)
        => _service.GetProductionScheduleAsync(companyId);

    [HttpGet("material-shortage-across-orders")]
    public Task<MaterialShortageAcrossOrdersDto> GetMaterialShortageAcrossOrdersAsync([FromQuery] Guid companyId)
        => _service.GetMaterialShortageAcrossOrdersAsync(companyId);

    [HttpPost("work-order/{workOrderId}/material-transfer")]
    public Task<StockEntryResultDto> CreateMaterialTransferForManufactureAsync(Guid workOrderId)
        => _service.CreateMaterialTransferForManufactureAsync(workOrderId);

    [HttpPost("manufacture-stock-entry")]
    public Task<StockEntryResultDto> CreateManufactureStockEntryAsync([FromBody] CreateManufactureStockEntryDto input)
        => _service.CreateManufactureStockEntryAsync(input);

    [HttpPost("work-order/disassembly")]
    public Task<DisassemblyResultDto> CreateDisassemblyStockEntryAsync([FromBody] CreateDisassemblyDto input)
        => _service.CreateDisassemblyStockEntryAsync(input);

    [HttpPost("work-order/create-from-sales-order/{salesOrderId}")]
    public Task<BatchCreateWorkOrdersResultDto> CreateWorkOrdersFromSalesOrderAsync(Guid salesOrderId)
        => _service.CreateWorkOrdersFromSalesOrderAsync(salesOrderId);

    [HttpGet("work-order/{workOrderId}/material-availability")]
    public Task<List<MaterialAvailabilityDto>> GetMaterialAvailabilityAsync(Guid workOrderId)
        => _service.GetMaterialAvailabilityAsync(workOrderId);

    [HttpGet("batch-material-readiness")]
    public Task<List<WorkOrderMaterialReadinessDto>> GetBatchMaterialReadinessAsync([FromQuery] Guid? companyId)
        => _service.GetBatchMaterialReadinessAsync(companyId);
}
