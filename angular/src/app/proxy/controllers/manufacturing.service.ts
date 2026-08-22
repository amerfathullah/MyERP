import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BatchCreateWorkOrdersResultDto, BomDto, CreateBomDto, CreateDisassemblyDto, CreateManufactureStockEntryDto, CreateMaterialConsumptionDto, CreateOperationDto, CreateRoutingDto, CreateWorkOrderDto, CreateWorkstationDto, DisassemblyResultDto, GetWorkOrderListDto, MaterialAvailabilityDto, MaterialConsumptionResultDto, MaterialShortageAcrossOrdersDto, OperationDto, ProductionCostBreakdownDto, ProductionScheduleDto, RoutingDto, StockEntryResultDto, SubcontractingBomItemsDto, WorkOrderDto, WorkOrderJobCardDto, WorkOrderMaterialReadinessDto, WorkstationDto, WorkstationUtilizationDto } from '../manufacturing/models';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ManufacturingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancelWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  createBom = (input: CreateBomDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomDto>({
      method: 'POST',
      url: '/api/app/manufacturing/bom',
      params: { itemId: input.itemId, quantity: input.quantity, uom: input.uom, companyId: input.companyId, isDefault: input.isDefault, sourceWarehouseId: input.sourceWarehouseId, targetWarehouseId: input.targetWarehouseId, routingId: input.routingId, scrapWarehouseId: input.scrapWarehouseId, processLossPercentage: input.processLossPercentage, items: input.items, operations: input.operations, secondaryItems: input.secondaryItems },
    },
    { apiName: this.apiName,...config });
  

  createDisassemblyStockEntry = (input: CreateDisassemblyDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DisassemblyResultDto>({
      method: 'POST',
      url: '/api/app/manufacturing/work-order/disassembly',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createJobCardsForWorkOrder = (workOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderJobCardDto[]>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${workOrderId}/create-job-cards`,
    },
    { apiName: this.apiName,...config });
  

  createManufactureStockEntry = (input: CreateManufactureStockEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryResultDto>({
      method: 'POST',
      url: '/api/app/manufacturing/manufacture-stock-entry',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createMaterialConsumption = (input: CreateMaterialConsumptionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialConsumptionResultDto>({
      method: 'POST',
      url: '/api/app/manufacturing/work-order/material-consumption',
      params: { workOrderId: input.workOrderId, items: input.items },
    },
    { apiName: this.apiName,...config });
  

  createMaterialTransferForManufacture = (workOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryResultDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${workOrderId}/material-transfer`,
    },
    { apiName: this.apiName,...config });
  

  createOperation = (input: CreateOperationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OperationDto>({
      method: 'POST',
      url: '/api/app/manufacturing/operations',
      params: { name: input.name, description: input.description, workstationId: input.workstationId, workstationType: input.workstationType, workstationTypeId: input.workstationTypeId, createJobCardBasedOnBatchSize: input.createJobCardBasedOnBatchSize, batchSize: input.batchSize, isCorrectiveOperation: input.isCorrectiveOperation, isActive: input.isActive, subOperations: input.subOperations },
    },
    { apiName: this.apiName,...config });
  

  createRouting = (input: CreateRoutingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RoutingDto>({
      method: 'POST',
      url: '/api/app/manufacturing/routings',
      params: { name: input.name, isDisabled: input.isDisabled, operations: input.operations },
    },
    { apiName: this.apiName,...config });
  

  createWorkOrder = (input: CreateWorkOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: '/api/app/manufacturing/work-order',
      params: { itemId: input.itemId, bomId: input.bomId, quantity: input.quantity, companyId: input.companyId, salesOrderId: input.salesOrderId, sourceWarehouseId: input.sourceWarehouseId, wipWarehouseId: input.wipWarehouseId, fgWarehouseId: input.fgWarehouseId, plannedStartDate: input.plannedStartDate, plannedEndDate: input.plannedEndDate, notes: input.notes },
    },
    { apiName: this.apiName,...config });
  

  createWorkOrdersFromSalesOrder = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchCreateWorkOrdersResultDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/create-from-sales-order/${salesOrderId}`,
    },
    { apiName: this.apiName,...config });
  

  createWorkstation = (input: CreateWorkstationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkstationDto>({
      method: 'POST',
      url: '/api/app/manufacturing/workstations',
      params: { companyId: input.companyId, name: input.name, workstationType: input.workstationType, workstationTypeId: input.workstationTypeId, productionCapacity: input.productionCapacity, description: input.description, costs: input.costs },
    },
    { apiName: this.apiName,...config });
  

  deleteBom = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/manufacturing/bom/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteOperation = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/manufacturing/operations/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteRouting = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/manufacturing/routings/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/manufacturing/work-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getBatchMaterialReadiness = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderMaterialReadinessDto[]>({
      method: 'GET',
      url: '/api/app/manufacturing/batch-material-readiness',
      params: { companyId },
    },
    { apiName: this.apiName,...config });
  

  getBom = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomDto>({
      method: 'GET',
      url: `/api/app/manufacturing/bom/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getBomItemsForSubcontracting = (itemId: string, companyId: string, fgQty: number = 1, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingBomItemsDto>({
      method: 'GET',
      url: '/api/app/manufacturing/bom/subcontracting-items',
      params: { itemId, companyId, fgQty },
    },
    { apiName: this.apiName,...config });
  

  getBomList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BomDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/bom',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getCapacityUtilization = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkstationUtilizationDto[]>({
      method: 'GET',
      url: '/api/app/manufacturing/workstations/capacity-utilization',
      params: { companyId },
    },
    { apiName: this.apiName,...config });
  

  getMaterialAvailability = (workOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialAvailabilityDto[]>({
      method: 'GET',
      url: `/api/app/manufacturing/work-order/${workOrderId}/material-availability`,
    },
    { apiName: this.apiName,...config });
  

  getMaterialShortageAcrossOrders = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialShortageAcrossOrdersDto>({
      method: 'GET',
      url: '/api/app/manufacturing/material-shortage-across-orders',
      params: { companyId },
    },
    { apiName: this.apiName,...config });
  

  getOperation = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OperationDto>({
      method: 'GET',
      url: `/api/app/manufacturing/operations/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getOperationList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OperationDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/operations',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getProductionCostBreakdown = (workOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionCostBreakdownDto>({
      method: 'GET',
      url: `/api/app/manufacturing/work-order/${workOrderId}/cost-breakdown`,
    },
    { apiName: this.apiName,...config });
  

  getProductionSchedule = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionScheduleDto>({
      method: 'GET',
      url: '/api/app/manufacturing/production-schedule',
      params: { companyId },
    },
    { apiName: this.apiName,...config });
  

  getRouting = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RoutingDto>({
      method: 'GET',
      url: `/api/app/manufacturing/routings/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getRoutingList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RoutingDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/routings',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'GET',
      url: `/api/app/manufacturing/work-order/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getWorkOrderJobCards = (workOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WorkOrderJobCardDto>>({
      method: 'GET',
      url: `/api/app/manufacturing/work-order/${workOrderId}/job-cards`,
    },
    { apiName: this.apiName,...config });
  

  getWorkOrderList = (input: GetWorkOrderListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WorkOrderDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/work-order',
      params: { status: input.status, filter: input.filter, companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWorkstation = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkstationDto>({
      method: 'GET',
      url: `/api/app/manufacturing/workstations/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getWorkstationList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WorkstationDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/workstations',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  recordProduction = (id: string, quantity: number, processLossQty?: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/record-production`,
      params: { quantity, processLossQty },
    },
    { apiName: this.apiName,...config });
  

  startWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/start`,
    },
    { apiName: this.apiName,...config });
  

  stopWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/stop`,
    },
    { apiName: this.apiName,...config });
  

  submitWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  unstopWorkOrder = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderDto>({
      method: 'POST',
      url: `/api/app/manufacturing/work-order/${id}/unstop`,
    },
    { apiName: this.apiName,...config });
  

  updateBom = (id: string, input: CreateBomDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomDto>({
      method: 'PUT',
      url: `/api/app/manufacturing/bom/${id}`,
      params: { itemId: input.itemId, quantity: input.quantity, uom: input.uom, companyId: input.companyId, isDefault: input.isDefault, sourceWarehouseId: input.sourceWarehouseId, targetWarehouseId: input.targetWarehouseId, routingId: input.routingId, scrapWarehouseId: input.scrapWarehouseId, processLossPercentage: input.processLossPercentage, items: input.items, operations: input.operations, secondaryItems: input.secondaryItems },
    },
    { apiName: this.apiName,...config });
  

  updateBomCost = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomDto>({
      method: 'POST',
      url: `/api/app/manufacturing/bom/${id}/update-cost`,
    },
    { apiName: this.apiName,...config });
  

  updateOperation = (id: string, input: CreateOperationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OperationDto>({
      method: 'PUT',
      url: `/api/app/manufacturing/operations/${id}`,
      params: { name: input.name, description: input.description, workstationId: input.workstationId, workstationType: input.workstationType, workstationTypeId: input.workstationTypeId, createJobCardBasedOnBatchSize: input.createJobCardBasedOnBatchSize, batchSize: input.batchSize, isCorrectiveOperation: input.isCorrectiveOperation, isActive: input.isActive, subOperations: input.subOperations },
    },
    { apiName: this.apiName,...config });
  

  updateRouting = (id: string, input: CreateRoutingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RoutingDto>({
      method: 'PUT',
      url: `/api/app/manufacturing/routings/${id}`,
      params: { name: input.name, isDisabled: input.isDisabled, operations: input.operations },
    },
    { apiName: this.apiName,...config });
  

  updateWorkstation = (id: string, input: CreateWorkstationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkstationDto>({
      method: 'PUT',
      url: `/api/app/manufacturing/workstations/${id}`,
      params: { companyId: input.companyId, name: input.name, workstationType: input.workstationType, workstationTypeId: input.workstationTypeId, productionCapacity: input.productionCapacity, description: input.description, costs: input.costs },
    },
    { apiName: this.apiName,...config });
}