import type { AuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { SalesForecastFrequency } from './sales-forecast-frequency.enum';
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

export interface BomCreatorDto extends EntityDto<string> {
  companyId?: string;
  finishedGoodItemId?: string;
  qty?: number;
  uom?: string | null;
  isPhantom?: boolean;
  routingId?: string | null;
  defaultWarehouseId?: string | null;
  rmCostAsPer?: string;
  rawMaterialCost?: number;
  remarks?: string | null;
  status?: number;
  errorLog?: string | null;
  items?: BomCreatorItemDto[];
}

export interface BomCreatorItemDto {
  itemId?: string;
  itemName?: string;
  fgItemId?: string;
  isExpandable?: boolean;
  qty?: number;
  rate?: number;
  amount?: number;
  uom?: string | null;
  conversionFactor?: number;
  stockUom?: string;
  operationId?: string | null;
  isSubcontracted?: boolean;
  isPhantomItem?: boolean;
  sourcedBySupplier?: boolean;
  instruction?: string | null;
  bomCreated?: boolean;
}

export interface BomMaterialAvailabilityDto {
  itemId?: string;
  itemName?: string;
  requiredQtyPerUnit?: number;
  requiredQtyForBatch?: number;
  availableQty?: number;
  shortage?: number;
  isSufficient?: boolean;
}

export interface BomStockAnalysisDto {
  bomId?: string;
  bomNumber?: string;
  itemName?: string;
  bomQuantity?: number;
  requestedQty?: number;
  canManufactureQty?: number;
  allMaterialsSufficient?: boolean;
  materials?: BomMaterialAvailabilityDto[];
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

export interface CreateMasterProductionScheduleDto {
  companyId?: string;
  postingDate?: string;
  fromDate?: string;
  parentWarehouseId?: string | null;
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

export interface CreateSalesForecastDto {
  companyId?: string;
  fromDate?: string;
  parentWarehouseId?: string;
  frequency?: SalesForecastFrequency;
  demandNumber?: number;
  selectedItemIds?: string[];
}

export interface CreateUpdateBomCreatorDto {
  companyId?: string;
  finishedGoodItemId?: string;
  qty?: number;
  uom?: string | null;
  isPhantom?: boolean;
  routingId?: string | null;
  defaultWarehouseId?: string | null;
  rmCostAsPer?: string;
  remarks?: string | null;
  items?: BomCreatorItemDto[];
}

export interface CreateUpdateDowntimeEntryDto {
  companyId?: string;
  workstationId?: string;
  operatorId?: string;
  fromTime?: string;
  toTime?: string;
  stopReason?: string;
  remarks?: string | null;
}

export interface CreateWorkstationTypeCostDto {
  component?: string;
  operatingCost?: number;
}

export interface CreateWorkstationTypeDto {
  name?: string;
  description?: string | null;
  costs?: CreateWorkstationTypeCostDto[];
}

export interface DailyProductionPointDto {
  date?: string;
  producedQty?: number;
}

export interface DowntimeEntryDto extends EntityDto<string> {
  companyId?: string;
  workstationId?: string;
  operatorId?: string;
  fromTime?: string;
  toTime?: string;
  downtimeMinutes?: number;
  stopReason?: string;
  remarks?: string | null;
}

export interface FetchMaterialRequestsDto {
  fromDate?: string | null;
  toDate?: string | null;
}

export interface FetchSalesOrdersDto {
  customerId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
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

export interface ManufacturingSettingsDto extends EntityDto<string> {
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

export interface MasterProductionScheduleDto extends EntityDto<string> {
  companyId?: string;
  scheduleNumber?: string;
  status?: number;
  postingDate?: string;
  fromDate?: string;
  toDate?: string | null;
  parentWarehouseId?: string | null;
  salesForecastId?: string | null;
  salesOrders?: MpsSalesOrderRefDto[];
  materialRequests?: MpsMaterialRequestRefDto[];
  items?: MasterProductionScheduleItemDto[];
}

export interface MasterProductionScheduleItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  bomId?: string | null;
  uom?: string;
  warehouseId?: string | null;
  deliveryDate?: string;
  plannedQty?: number;
  cumulativeLeadTimeDays?: number;
  orderReleaseDate?: string;
}

export interface MpsMaterialRequestRefDto {
  materialRequestId?: string;
  materialRequestDate?: string;
}

export interface MpsSalesOrderRefDto {
  salesOrderId?: string;
  salesOrderDate?: string;
  customerId?: string;
  grandTotal?: number;
  status?: string | null;
}

export interface ProductionAnalyticsDto {
  totalWorkOrders?: number;
  completedCount?: number;
  inProcessCount?: number;
  overdueCount?: number;
  completionRate?: number;
  totalPlannedQty?: number;
  totalProducedQty?: number;
  productionEfficiency?: number;
  statusBreakdown?: ProductionStatusCountDto[];
  dailyTrend?: DailyProductionPointDto[];
  topProducedItems?: TopProducedItemDto[];
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

export interface ProductionStatusCountDto {
  status?: string;
  count?: number;
  color?: string;
}

export interface SalesForecastDto extends EntityDto<string> {
  companyId?: string;
  forecastNumber?: string;
  status?: number;
  forecastStatus?: string;
  postingDate?: string;
  fromDate?: string;
  frequency?: string;
  demandNumber?: number;
  parentWarehouseId?: string;
  selectedItemIds?: string[];
  items?: SalesForecastItemDto[];
}

export interface SalesForecastItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  uom?: string;
  warehouseId?: string | null;
  deliveryDate?: string;
  demandQty?: number;
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

export interface TopProducedItemDto {
  itemId?: string;
  itemName?: string;
  totalProduced?: number;
  workOrderCount?: number;
}

export interface UpdateMasterProductionScheduleDto {
  postingDate?: string;
  fromDate?: string;
  parentWarehouseId?: string | null;
  items?: MasterProductionScheduleItemDto[];
}

export interface UpdateSalesForecastDto {
  fromDate?: string;
  parentWarehouseId?: string;
  frequency?: SalesForecastFrequency;
  demandNumber?: number;
  selectedItemIds?: string[];
}

export interface WorkstationTypeCostDto {
  component?: string;
  operatingCost?: number;
}

export interface WorkstationTypeDto extends EntityDto<string> {
  name?: string;
  description?: string | null;
  hourRate?: number;
  costs?: WorkstationTypeCostDto[];
}

export interface ActiveJobOnWorkstationDto {
  jobCardId?: string;
  workOrderNumber?: string | null;
  itemName?: string | null;
  operationName?: string | null;
  forQuantity?: number;
  completedQty?: number;
  plannedTimeInMins?: number;
  actualTimeInMins?: number;
  status?: number;
}

export interface BatchCreateWorkOrdersResultDto {
  createdCount?: number;
  skippedCount?: number;
  workOrders?: CreatedWorkOrderInfo[];
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

export interface CreateDisassemblyDto {
  workOrderId: string;
  quantity: number;
  sourceStockEntryId?: string | null;
}

export interface CreateManufactureStockEntryDto {
  workOrderId: string;
  fgQuantity: number;
  processLossQty?: number;
}

export interface CreateMaterialConsumptionDto {
  workOrderId: string;
  items: ConsumptionItemDto[];
}

export interface CreateOperationDto {
  name?: string;
  description?: string | null;
  workstationId?: string | null;
  workstationType?: string | null;
  workstationTypeId?: string | null;
  createJobCardBasedOnBatchSize?: boolean;
  batchSize?: number;
  isCorrectiveOperation?: boolean;
  isActive?: boolean;
  subOperations?: CreateSubOperationDto[];
}

export interface CreateRoutingDto {
  name?: string;
  isDisabled?: boolean;
  operations?: CreateRoutingOperationDto[];
}

export interface CreateRoutingOperationDto {
  operationId?: string;
  sequenceId?: number;
  timeInMins?: number;
  workstationId?: string | null;
}

export interface CreateSubOperationDto {
  operationId?: string;
  timeInMins?: number;
  description?: string | null;
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

export interface CreateWorkstationCostDto {
  component?: string;
  operatingCost?: number;
}

export interface CreateWorkstationDto {
  companyId?: string;
  name?: string;
  workstationType?: string | null;
  workstationTypeId?: string | null;
  productionCapacity?: number;
  description?: string | null;
  costs?: CreateWorkstationCostDto[];
}

export interface CreatedWorkOrderInfo {
  workOrderId?: string;
  workOrderNumber?: string;
  itemName?: string | null;
  quantity?: number;
}

export interface DisassemblyResultDto {
  stockEntryId?: string;
  entryNumber?: string;
  disassembledQty?: number;
  itemCount?: number;
  remainingDisassemblable?: number;
}

export interface GetWorkOrderListDto extends PagedAndSortedResultRequestDto {
  status?: WorkOrderStatus | null;
  filter?: string | null;
  companyId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface MaterialAvailabilityDto {
  itemId?: string;
  itemName?: string;
  itemCode?: string;
  requiredQty?: number;
  transferredQty?: number;
  pendingQty?: number;
  availableQty?: number;
  shortage?: number;
  hasSufficientStock?: boolean;
  warehouseId?: string;
}

export interface MaterialConsumptionResultDto {
  stockEntryId?: string;
  entryNumber?: string;
  totalConsumedValue?: number;
  itemCount?: number;
}

export interface MaterialShortageAcrossOrdersDto {
  items?: MaterialShortageItemDto[];
  totalItemsShort?: number;
  totalAffectedOrders?: number;
  totalShortageValue?: number;
}

export interface MaterialShortageItemDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  totalRequired?: number;
  totalAvailable?: number;
  shortageQty?: number;
  affectedWorkOrders?: number;
  mostUrgentWO?: string;
}

export interface OperationDto extends EntityDto<string> {
  name?: string;
  description?: string | null;
  workstationId?: string | null;
  workstationType?: string | null;
  workstationTypeId?: string | null;
  createJobCardBasedOnBatchSize?: boolean;
  batchSize?: number;
  isCorrectiveOperation?: boolean;
  isActive?: boolean;
  totalOperationTime?: number;
  subOperations?: SubOperationDto[];
}

export interface ProductionCostBreakdownDto {
  workOrderId?: string;
  workOrderNumber?: string | null;
  itemId?: string;
  itemName?: string | null;
  producedQty?: number;
  totalRmCost?: number;
  processLossQty?: number;
  processLossValue?: number;
  additionalCosts?: number;
  totalProductionCost?: number;
  fgUnitCost?: number;
  bomStandardCost?: number;
  costVariance?: number;
  costVariancePercent?: number;
}

export interface ProductionScheduleDto {
  items?: ProductionScheduleItemDto[];
  totalOrders?: number;
  notStarted?: number;
  inProcess?: number;
  completed?: number;
  overdue?: number;
  overallCompletionRate?: number;
}

export interface ProductionScheduleItemDto {
  workOrderId?: string;
  workOrderNumber?: string;
  itemName?: string;
  quantity?: number;
  producedQuantity?: number;
  percentComplete?: number;
  status?: number;
  statusLabel?: string;
  plannedStartDate?: string | null;
  plannedEndDate?: string | null;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  isOverdue?: boolean;
  daysOverdue?: number;
  statusColor?: string;
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

export interface StockEntryResultDto {
  stockEntryId?: string;
  entryNumber?: string | null;
  entryType?: string | null;
  itemCount?: number;
  totalValue?: number;
}

export interface SubOperationDto {
  id?: string;
  operationId?: string;
  timeInMins?: number;
  description?: string | null;
}

export interface SubcontractingBomItemLineDto {
  itemId?: string;
  itemName?: string;
  itemCode?: string;
  requiredQty?: number;
  rate?: number;
  uom?: string;
  sourceWarehouseId?: string | null;
}

export interface SubcontractingBomItemsDto {
  bomId?: string | null;
  bomNumber?: string | null;
  fgItemId?: string | null;
  fgQty?: number;
  sourceWarehouseId?: string | null;
  items?: SubcontractingBomItemLineDto[];
}

export interface WorkOrderDto extends AuditedEntityDto<string> {
  workOrderNumber?: string;
  status?: WorkOrderStatus;
  itemId?: string;
  itemName?: string | null;
  bomId?: string;
  quantity?: number;
  producedQuantity?: number;
  disassembledQuantity?: number;
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

export interface WorkOrderJobCardDto {
  id?: string;
  sequenceId?: number;
  operationId?: string;
  status?: number;
  forQuantity?: number;
  completedQty?: number;
  totalTimeInMins?: number;
  plannedTimeInMins?: number;
  operationName?: string | null;
}

export interface WorkOrderMaterialReadinessDto {
  workOrderId?: string;
  workOrderNumber?: string;
  itemName?: string;
  isReady?: boolean;
  isPartial?: boolean;
  hasShortage?: boolean;
  totalMaterials?: number;
  materialsAvailable?: number;
  materialsShort?: number;
  totalShortageValue?: number;
  readinessStatus?: string;
}

export interface WorkstationCostDto {
  name?: string;
  amount?: number;
}

export interface WorkstationDto extends EntityDto<string> {
  name?: string;
  workstationType?: string | null;
  workstationTypeId?: string | null;
  productionCapacity?: number;
  hourRate?: number;
  description?: string | null;
  isActive?: boolean;
  costs?: WorkstationCostDto[];
  workingHours?: WorkstationWorkingHourDto[];
}

export interface WorkstationUtilizationDto {
  workstationId?: string;
  workstationName?: string;
  workstationType?: string | null;
  productionCapacity?: number;
  activeJobCards?: number;
  utilizationPercent?: number;
  totalPlannedMinutes?: number;
  totalActualMinutes?: number;
  status?: string;
  activeJobs?: ActiveJobOnWorkstationDto[];
}

export interface WorkstationWorkingHourDto {
  dayOfWeek?: string;
  startTime?: string;
  endTime?: string;
}

export interface PlantFloorDto extends AuditedEntityDto<string> {
  companyId?: string;
  companyName?: string | null;
  floorName: string;
  warehouseId?: string | null;
  warehouseName?: string | null;
  description?: string | null;
  isActive: boolean;
}

export interface CreateUpdatePlantFloorDto {
  companyId: string;
  floorName: string;
  warehouseId?: string | null;
  description?: string | null;
  isActive?: boolean;
}

export interface GetPlantFloorListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  filter?: string | null;
}
