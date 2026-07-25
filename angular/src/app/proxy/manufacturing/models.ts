import type { AuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { JobCardStatus } from './job-card-status.enum';
import type { ProductionPlanStatus } from './production-plan-status.enum';
import type { SubAssemblyType } from './sub-assembly-type.enum';
import type { SecondaryItemType } from './secondary-item-type.enum';
import type { WorkOrderStatus } from './work-order-status.enum';

export interface AddTimeLogDto {
  fromTime?: string;
  toTime?: string;
  completedQty?: number;
}

export interface CreateJobCardDto {
  companyId?: string;
  workOrderId?: string;
  operationId?: string;
  workstationId?: string | null;
  forQuantity?: number;
  sequenceId?: number;
  plannedTimeInMins?: number;
}

export interface CreateOperationDto {
  name?: string;
  description?: string | null;
  workstationId?: string | null;
  workstationType?: string | null;
  createJobCardBasedOnBatchSize?: boolean;
  batchSize?: number;
}

export interface CreateProductionPlanDto {
  companyId: string;
  postingDate?: string;
  combineItems?: boolean;
  ignoreExistingOrderedQty?: boolean;
  considerMinimumOrderQty?: boolean;
  includeSafetyStock?: boolean;
  skipAvailableSubAssemblyItem?: boolean;
  rawMaterialGroupWarehouseId?: string | null;
  forWarehouseId?: string | null;
  notes?: string | null;
  items?: CreateProductionPlanItemDto[];
}

export interface CreateProductionPlanItemDto {
  itemId: string;
  itemName: string;
  bomId: string;
  plannedQty?: number;
  warehouseId?: string | null;
  plannedStartDate?: string | null;
  salesOrderId?: string | null;
  materialRequestId?: string | null;
}

export interface CreateRoutingDto {
  name?: string;
  operations?: CreateRoutingOperationDto[];
}

export interface CreateRoutingOperationDto {
  operationId?: string;
  sequenceId?: number;
  timeInMins?: number;
  workstationId?: string | null;
}

export interface GetJobCardListDto extends PagedAndSortedResultRequestDto {
  workOrderId?: string | null;
  companyId?: string | null;
  status?: JobCardStatus | null;
  filter?: string | null;
}

export interface GetProductionPlanListDto extends PagedAndSortedResultRequestDto {
  status?: ProductionPlanStatus | null;
  companyId?: string | null;
  filter?: string | null;
}

export interface JobCardDto extends EntityDto<string> {
  companyId?: string;
  workOrderId?: string;
  operationId?: string;
  bomOperationId?: string | null;
  workstationId?: string | null;
  finishedGoodItemId?: string | null;
  semiFgBomId?: string | null;
  isCorrective?: boolean;
  forQuantity?: number;
  completedQty?: number;
  totalTimeInMins?: number;
  plannedTimeInMins?: number;
  sequenceId?: number;
  status?: number;
  startedAt?: string | null;
  completedAt?: string | null;
  timeLogs?: JobCardTimeLogDto[];
  creationTime?: string;
}

export interface JobCardTimeLogDto {
  id?: string;
  fromTime?: string;
  toTime?: string;
  timeInMins?: number;
  completedQty?: number;
}

export interface ManufacturingSettingsDto {
  id?: string;
  companyId?: string;
  overproductionPercentage?: number;
  backflushRawMaterialsBasedOn?: string;
  materialConsumption?: boolean;
  transferExtraMaterialsPercentage?: number;
  minsBetweenOperations?: number;
  capacityPlanningForDays?: number;
  makeSerialNoBatchFromWorkOrder?: boolean;
  updateBomCostsAutomatically?: boolean;
  allowOvertime?: boolean;
  allowProductionOnHolidays?: boolean;
  disableCapacityPlanning?: boolean;
  jobCardExcessTransfer?: boolean;
  enforceTimeLogs?: boolean;
  addCorrectiveOpCostInFGValuation?: boolean;
  validateComponentsQuantitiesPerBom?: boolean;
}

export interface OperationDto extends EntityDto<string> {
  name?: string;
  description?: string | null;
  workstationId?: string | null;
  workstationType?: string | null;
  createJobCardBasedOnBatchSize?: boolean;
  batchSize?: number;
  isCorrectiveOperation?: boolean;
  isActive?: boolean;
}

export interface ProductionPlanDto extends AuditedEntityDto<string> {
  planNumber?: string;
  status?: ProductionPlanStatus;
  companyId?: string;
  postingDate?: string;
  combineItems?: boolean;
  ignoreExistingOrderedQty?: boolean;
  considerMinimumOrderQty?: boolean;
  includeSafetyStock?: boolean;
  skipAvailableSubAssemblyItem?: boolean;
  rawMaterialGroupWarehouseId?: string | null;
  forWarehouseId?: string | null;
  notes?: string | null;
  plannedItems?: ProductionPlanItemDto[];
  materialRequirements?: ProductionPlanMrItemDto[];
}

export interface ProductionPlanItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  bomId?: string;
  plannedQty?: number;
  producedQty?: number;
  warehouseId?: string | null;
  plannedStartDate?: string | null;
  salesOrderId?: string | null;
  materialRequestId?: string | null;
  workOrderId?: string | null;
}

export interface ProductionPlanMrItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  requiredQty?: number;
  orderedQty?: number;
  availableQty?: number;
  plannedQty?: number;
  minOrderQty?: number;
  safetyStock?: number;
  uom?: string | null;
  warehouseId?: string | null;
  materialRequestId?: string | null;
  procurementType?: SubAssemblyType;
}

export interface RoutingDto extends EntityDto<string> {
  name?: string;
  isDisabled?: boolean;
  operations?: RoutingOperationDto[];
}

export interface RoutingOperationDto {
  id?: string;
  operationId?: string;
  sequenceId?: number;
  timeInMins?: number;
  workstationId?: string | null;
  operatingCost?: number;
}

export interface SaveManufacturingSettingsDto {
  companyId?: string;
  overproductionPercentage?: number;
  backflushRawMaterialsBasedOn?: string;
  materialConsumption?: boolean;
  transferExtraMaterialsPercentage?: number;
  minsBetweenOperations?: number;
  capacityPlanningForDays?: number;
  makeSerialNoBatchFromWorkOrder?: boolean;
  updateBomCostsAutomatically?: boolean;
  allowOvertime?: boolean;
  allowProductionOnHolidays?: boolean;
  disableCapacityPlanning?: boolean;
  jobCardExcessTransfer?: boolean;
  enforceTimeLogs?: boolean;
  addCorrectiveOpCostInFGValuation?: boolean;
  validateComponentsQuantitiesPerBom?: boolean;
}

export interface BomDto extends AuditedEntityDto<string> {
  bomNumber?: string;
  itemId?: string;
  itemName?: string | null;
  quantity?: number;
  uom?: string | null;
  companyId?: string;
  isActive?: boolean;
  isDefault?: boolean;
  totalMaterialCost?: number;
  operatingCost?: number;
  totalCost?: number;
  processLossPercentage?: number;
  fgCostAllocationPercentage?: number;
  scrapWarehouseId?: string | null;
  items?: BomItemDto[];
  operations?: BomOperationDto[];
  secondaryItems?: BomSecondaryItemDto[];
}

export interface BomItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  quantity?: number;
  uom?: string | null;
  rate?: number;
  amount?: number;
}

export interface BomOperationDto {
  id?: string;
  operationId?: string;
  workstationId?: string | null;
  sequenceId?: number;
  timeInMins?: number;
  operatingCost?: number;
  batchSize?: number;
  fixedTime?: number;
  description?: string | null;
  isSubcontracted?: boolean;
}

export interface BomSecondaryItemDto {
  id?: string;
  itemId?: string;
  itemName?: string | null;
  secondaryItemType?: SecondaryItemType;
  quantity?: number;
  effectiveQuantity?: number;
  stockUom?: string | null;
  rate?: number;
  amount?: number;
  costAllocationPercentage?: number;
  processLossPercentage?: number;
  warehouseId?: string | null;
}

export interface ConsumptionItemDto {
  itemId: string;
  quantity: number;
  warehouseId?: string | null;
  batchId?: string | null;
}

export interface CreateBomDto {
  itemId: string;
  quantity?: number;
  uom?: string | null;
  companyId: string;
  isDefault?: boolean;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  routingId?: string | null;
  scrapWarehouseId?: string | null;
  processLossPercentage?: number;
  items?: CreateBomItemDto[];
  operations?: CreateBomOperationDto[];
  secondaryItems?: CreateBomSecondaryItemDto[];
}

export interface CreateBomItemDto {
  itemId: string;
  itemName: string;
  quantity?: number;
  uom?: string | null;
  rate?: number;
}

export interface CreateBomOperationDto {
  operationId: string;
  workstationId?: string | null;
  sequenceId?: number;
  timeInMins?: number;
  batchSize?: number;
  fixedTime?: number;
  description?: string | null;
  isSubcontracted?: boolean;
  workstationHourRate?: number;
}

export interface CreateBomSecondaryItemDto {
  itemId: string;
  itemName?: string | null;
  secondaryItemType?: SecondaryItemType;
  quantity?: number;
  stockUom?: string | null;
  rate?: number;
  costAllocationPercentage?: number;
  processLossPercentage?: number;
  warehouseId?: string | null;
}

export interface CreateMaterialConsumptionDto {
  workOrderId: string;
  items: ConsumptionItemDto[];
}

export interface CreateWorkOrderDto {
  itemId: string;
  bomId: string;
  quantity: number;
  companyId: string;
  salesOrderId?: string | null;
  sourceWarehouseId?: string | null;
  wipWarehouseId?: string | null;
  fgWarehouseId?: string | null;
  plannedStartDate?: string | null;
  plannedEndDate?: string | null;
  notes?: string | null;
}

export interface CreateWorkstationDto {
  companyId?: string;
  name?: string;
  workstationType?: string | null;
  productionCapacity?: number;
  description?: string | null;
}

export interface GetWorkOrderListDto extends PagedAndSortedResultRequestDto {
  status?: WorkOrderStatus | null;
  filter?: string | null;
  companyId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface MaterialConsumptionResultDto {
  stockEntryId?: string;
  entryNumber?: string;
  totalConsumedValue?: number;
  itemCount?: number;
}

export interface WorkOrderDto extends AuditedEntityDto<string> {
  workOrderNumber?: string;
  status?: WorkOrderStatus;
  itemId?: string;
  itemName?: string | null;
  bomId?: string;
  quantity?: number;
  producedQuantity?: number;
  materialTransferred?: number;
  processLossQty?: number;
  processLossPercentage?: number;
  effectiveFgQuantity?: number;
  percentComplete?: number;
  companyId?: string;
  salesOrderId?: string | null;
  plannedStartDate?: string | null;
  plannedEndDate?: string | null;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  notes?: string | null;
  requiredItems?: WorkOrderItemDto[];
}

export interface WorkOrderItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  requiredQuantity?: number;
  transferredQuantity?: number;
  consumedQuantity?: number;
}

export interface WorkstationCostDto {
  name?: string;
  amount?: number;
}

export interface WorkstationDto extends EntityDto<string> {
  name?: string;
  workstationType?: string | null;
  productionCapacity?: number;
  hourRate?: number;
  description?: string | null;
  isActive?: boolean;
  costs?: WorkstationCostDto[];
  workingHours?: WorkstationWorkingHourDto[];
}

export interface WorkstationWorkingHourDto {
  dayOfWeek?: string;
  startTime?: string;
  endTime?: string;
}
