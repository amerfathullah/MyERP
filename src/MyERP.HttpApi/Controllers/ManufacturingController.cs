using System;
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

    public ManufacturingController(IManufacturingAppService service, IWorkstationAppService workstationService)
    {
        _service = service;
        _workstationService = workstationService;
    }

    // Workstations
    [HttpGet("workstations/{id}")]
    public Task<WorkstationDto> GetWorkstationAsync(Guid id) => _workstationService.GetAsync(id);

    [HttpGet("workstations")]
    public Task<PagedResultDto<WorkstationDto>> GetWorkstationListAsync([FromQuery] CompanyFilteredPagedRequestDto input) => _workstationService.GetListAsync(input);

    [HttpPost("workstations")]
    public Task<WorkstationDto> CreateWorkstationAsync(CreateWorkstationDto input) => _workstationService.CreateAsync(input);

    // BOM
    [HttpGet("bom/{id}")]
    public Task<BomDto> GetBomAsync(Guid id) => _service.GetBomAsync(id);

    [HttpGet("bom")]
    public Task<PagedResultDto<BomDto>> GetBomListAsync([FromQuery] MyERP.Shared.CompanyFilteredPagedRequestDto input) => _service.GetBomListAsync(input);

    [HttpPost("bom")]
    public Task<BomDto> CreateBomAsync(CreateBomDto input) => _service.CreateBomAsync(input);

    [HttpPut("bom/{id}")]
    public Task<BomDto> UpdateBomAsync(Guid id, CreateBomDto input) => _service.UpdateBomAsync(id, input);

    [HttpDelete("bom/{id}")]
    public Task DeleteBomAsync(Guid id) => _service.DeleteBomAsync(id);

    // Work Order
    [HttpGet("work-order/{id}")]
    public Task<WorkOrderDto> GetWorkOrderAsync(Guid id) => _service.GetWorkOrderAsync(id);

    [HttpGet("work-order")]
    public Task<PagedResultDto<WorkOrderDto>> GetWorkOrderListAsync([FromQuery] GetWorkOrderListDto input) => _service.GetWorkOrderListAsync(input);

    [HttpPost("work-order")]
    public Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderDto input) => _service.CreateWorkOrderAsync(input);

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
    public Task<MaterialConsumptionResultDto> CreateMaterialConsumptionAsync(CreateMaterialConsumptionDto input) => _service.CreateMaterialConsumptionAsync(input);
}
