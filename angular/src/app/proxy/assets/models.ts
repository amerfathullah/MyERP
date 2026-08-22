import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AssetActivityType } from './asset-activity-type.enum';
import type { AssetCapitalizationStatus } from './asset-capitalization-status.enum';
import type { DepreciationMethod } from './depreciation-method.enum';
import type { AssetStatus } from './asset-status.enum';
import type { MaintenancePeriodicity } from '../maintenance/maintenance-periodicity.enum';
import type { AssetMaintenanceStatus } from '../maintenance/asset-maintenance-status.enum';
import type { AssetMovementPurpose } from './asset-movement-purpose.enum';
import type { DocumentStatus } from '../core/document-status.enum';
import type { AssetRepairStatus } from './asset-repair-status.enum';
import type { VehicleFuelType } from './vehicle-fuel-type.enum';
import type { DriverStatus } from './driver-status.enum';
import type { MaintenanceVisitStatus } from '../maintenance/maintenance-visit-status.enum';

export interface AssetActivityDto extends FullAuditedEntityDto<string> {
  assetId?: string;
  activityType?: AssetActivityType;
  subject?: string;
  details?: string | null;
  transactionDate?: string;
  referenceType?: string | null;
  referenceId?: string | null;
}

export interface AssetCapitalizationAssetItemDto extends EntityDto<string> {
  assetId?: string;
  assetName?: string;
  currentValue?: number;
}

export interface AssetCapitalizationDto extends FullAuditedEntityDto<string> {
  capitalizationNumber?: string;
  companyId?: string;
  postingDate?: string;
  targetAssetId?: string;
  targetAssetName?: string | null;
  totalCapitalizedAmount?: number;
  status?: AssetCapitalizationStatus;
  stockItems?: AssetCapitalizationStockItemDto[];
  serviceItems?: AssetCapitalizationServiceItemDto[];
  consumedAssets?: AssetCapitalizationAssetItemDto[];
}

export interface AssetCapitalizationServiceItemDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string;
  amount?: number;
  expenseAccountId?: string | null;
}

export interface AssetCapitalizationStockItemDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string;
  qty?: number;
  rate?: number;
  amount?: number;
  warehouseId?: string | null;
}

export interface AssetCategoryAccountDto extends FullAuditedEntityDto<string> {
  assetCategoryId?: string;
  companyId?: string;
  fixedAssetAccountId?: string;
  accumulatedDepreciationAccountId?: string | null;
  depreciationExpenseAccountId?: string | null;
  capitalWorkInProgressAccountId?: string | null;
}

export interface AssetCategoryDto extends FullAuditedEntityDto<string> {
  categoryName?: string;
  isDepreciable?: boolean;
  enableCwipAccounting?: boolean;
  nonDepreciableCategory?: boolean;
  defaultDepreciationMethod?: DepreciationMethod;
  defaultUsefulLifeMonths?: number;
  defaultDepreciationRate?: number | null;
  defaultFrequencyMonths?: number;
  assetAccountId?: string | null;
  depreciationAccountId?: string | null;
  accumulatedDepreciationAccountId?: string | null;
  accounts?: AssetCategoryAccountDto[];
}

export interface AssetDto extends FullAuditedEntityDto<string> {
  assetNumber?: string;
  assetName?: string;
  status?: AssetStatus;
  companyId?: string;
  assetCategoryId?: string | null;
  assetCategoryName?: string | null;
  itemId?: string | null;
  location?: string | null;
  locationId?: string | null;
  custodianEmployeeId?: string | null;
  purchaseDate?: string;
  purchaseAmount?: number;
  additionalCost?: number;
  totalAssetCost?: number;
  purchaseReceiptId?: string | null;
  purchaseInvoiceId?: string | null;
  calculateDepreciation?: boolean;
  depreciationMethod?: DepreciationMethod;
  usefulLifeMonths?: number;
  depreciationRate?: number;
  frequencyMonths?: number;
  availableForUseDate?: string | null;
  openingAccumulatedDepreciation?: number;
  valueAfterDepreciation?: number;
  isFullyDepreciated?: boolean;
  disposalDate?: string | null;
  disposalAmount?: number | null;
  notes?: string | null;
  assetQuantity?: number;
  splitFromAssetId?: string | null;
  schedule?: DepreciationScheduleDto[];
}

export interface AssetMaintenanceDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  assetId?: string;
  assetName?: string | null;
  itemId?: string | null;
  itemCode?: string | null;
  itemName?: string | null;
  maintenanceManagerId?: string | null;
  maintenanceManagerName?: string | null;
  maintenanceTeamId?: string | null;
  maintenanceTeamName?: string | null;
  tasks?: AssetMaintenanceTaskDto[];
}

export interface AssetMaintenanceLogDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  assetMaintenanceId?: string;
  assetMaintenanceTaskId?: string;
  assetId?: string;
  assetName?: string | null;
  itemId?: string | null;
  itemCode?: string | null;
  itemName?: string | null;
  maintenanceTask?: string;
  periodicity?: MaintenancePeriodicity;
  maintenanceType?: string | null;
  dueDate?: string;
  completionDate?: string | null;
  status?: AssetMaintenanceStatus;
  assignToEmployeeId?: string | null;
  assignTo?: string | null;
  assignToName?: string | null;
  hasCertificate?: boolean;
  certificateDetails?: string | null;
  certificateNo?: string | null;
  cost?: number | null;
  description?: string | null;
  actionsPerformed?: string | null;
  remarks?: string | null;
}

export interface AssetMaintenanceTaskDto extends FullAuditedEntityDto<string> {
  assetMaintenanceId?: string;
  maintenanceTask?: string;
  periodicity?: MaintenancePeriodicity;
  maintenanceType?: string | null;
  startDate?: string;
  endDate?: string | null;
  nextDueDate?: string;
  lastCompletionDate?: string | null;
  assignToEmployeeId?: string | null;
  assignTo?: string | null;
  assignToName?: string | null;
  certificateRequired?: boolean;
  description?: string | null;
  certificateNo?: string | null;
}

export interface AssetMaintenanceTeamDto extends EntityDto<string> {
  companyId?: string;
  teamName?: string;
  maintenanceManagerId?: string | null;
  members?: AssetMaintenanceTeamMemberDto[];
}

export interface AssetMaintenanceTeamMemberDto {
  employeeId?: string;
  employeeName?: string | null;
  maintenanceRole?: string | null;
}

export interface AssetMovementDto extends FullAuditedEntityDto<string> {
  movementNumber?: string;
  companyId?: string;
  purpose?: AssetMovementPurpose;
  transactionDate?: string;
  referenceType?: string | null;
  referenceId?: string | null;
  assetId?: string | null;
  sourceLocation?: string | null;
  sourceLocationId?: string | null;
  sourceEmployeeId?: string | null;
  targetLocation?: string | null;
  targetLocationId?: string | null;
  targetEmployeeId?: string | null;
  status?: DocumentStatus;
  items?: AssetMovementItemDto[];
}

export interface AssetMovementItemDto extends FullAuditedEntityDto<string> {
  assetMovementId?: string;
  assetId?: string;
  assetName?: string | null;
  sourceLocation?: string | null;
  sourceLocationId?: string | null;
  targetLocation?: string | null;
  targetLocationId?: string | null;
  fromEmployeeId?: string | null;
  toEmployeeId?: string | null;
}

export interface AssetRepairConsumedItemDto extends FullAuditedEntityDto<string> {
  assetRepairId?: string;
  itemId?: string;
  itemName?: string | null;
  warehouseId?: string | null;
  qty?: number;
  valuationRate?: number;
  totalValue?: number;
  serialAndBatchBundleId?: string | null;
}

export interface AssetRepairDto extends FullAuditedEntityDto<string> {
  repairNumber?: string;
  companyId?: string;
  assetId?: string;
  assetName?: string | null;
  repairDescription?: string | null;
  actionsPerformed?: string | null;
  downtime?: string | null;
  failureDate?: string | null;
  completionDate?: string | null;
  costCenterId?: string | null;
  projectId?: string | null;
  repairCost?: number;
  consumedItemsCost?: number;
  totalRepairCost?: number;
  capitalizeRepairCost?: boolean;
  increaseInAssetLife?: number;
  status?: AssetRepairStatus;
  stockItems?: AssetRepairConsumedItemDto[];
  invoices?: AssetRepairPurchaseInvoiceDto[];
}

export interface AssetRepairPurchaseInvoiceDto extends FullAuditedEntityDto<string> {
  assetRepairId?: string;
  purchaseInvoiceId?: string;
  purchaseInvoiceNumber?: string | null;
  repairCost?: number;
  expenseAccountId?: string | null;
}

export interface AssetShiftAllocationDto extends FullAuditedEntityDto<string> {
  allocationNumber?: string;
  assetId?: string;
  financeBookId?: string | null;
  status?: DocumentStatus;
  lines?: AssetShiftAllocationLineDto[];
}

export interface AssetShiftAllocationLineDto extends EntityDto<string> {
  scheduleEntryId?: string;
  shiftFactorId?: string;
  shiftFactorName?: string | null;
  scheduleDate?: string;
  depreciationAmount?: number;
  accumulatedDepreciation?: number;
}

export interface AssetShiftFactorDto extends FullAuditedEntityDto<string> {
  shiftName?: string;
  factor?: number;
  isDefault?: boolean;
}

export interface AssetValueAdjustmentDto extends FullAuditedEntityDto<string> {
  adjustmentNumber?: string;
  companyId?: string;
  assetId?: string;
  assetName?: string | null;
  financeBookId?: string | null;
  date?: string;
  currentAssetValue?: number;
  newAssetValue?: number;
  differenceAmount?: number;
  differenceAccountId?: string;
  costCenterId?: string | null;
  journalEntryId?: string | null;
  notes?: string | null;
  status?: DocumentStatus;
}

export interface AssignShiftLineDto {
  scheduleEntryId: string;
  shiftFactorId: string;
}

export interface CompleteAssetMaintenanceLogDto {
  completionDate?: string;
  actionsPerformed?: string | null;
  certificateNo?: string | null;
  hasCertificate?: boolean;
  certificateDetails?: string | null;
  cost?: number | null;
  remarks?: string | null;
}

export interface CreateAssetActivityDto {
  assetId?: string;
  activityType?: AssetActivityType;
  subject: string;
  details?: string | null;
  transactionDate?: string;
  referenceType?: string | null;
  referenceId?: string | null;
}

export interface CreateAssetDto {
  assetName: string;
  companyId: string;
  assetCategoryId?: string | null;
  itemId?: string | null;
  location?: string | null;
  locationId?: string | null;
  custodianEmployeeId?: string | null;
  purchaseDate: string;
  purchaseAmount?: number;
  additionalCost?: number;
  calculateDepreciation?: boolean;
  depreciationMethod?: DepreciationMethod;
  usefulLifeMonths?: number;
  depreciationRate?: number;
  frequencyMonths?: number;
  availableForUseDate?: string | null;
  openingAccumulatedDepreciation?: number;
  notes?: string | null;
}

export interface CreateAssetShiftAllocationDto {
  assetId: string;
  financeBookId?: string | null;
  lines: AssignShiftLineDto[];
}

export interface CreateMaintenanceScheduleDto {
  companyId?: string;
  assetId?: string | null;
  itemId?: string | null;
  customerId?: string | null;
  startDate?: string;
  endDate?: string;
  periodicity?: string;
}

export interface CreateMaintenanceVisitDto {
  companyId?: string;
  visitDate?: string;
  maintenanceType?: string;
  maintenanceScheduleId?: string | null;
  customerId?: string | null;
  contactId?: string | null;
  purposes?: CreateMaintenanceVisitPurposeDto[];
}

export interface CreateMaintenanceVisitPurposeDto {
  itemId?: string | null;
  itemName?: string | null;
  serialNoId?: string | null;
  workDone?: string;
  workDetails?: string | null;
}

export interface CreateUpdateAssetCapitalizationAssetItemDto {
  assetId?: string;
  assetName?: string;
  currentValue?: number;
}

export interface CreateUpdateAssetCapitalizationDto {
  companyId?: string;
  postingDate?: string;
  targetAssetId?: string;
  targetAssetName?: string | null;
  stockItems?: CreateUpdateAssetCapitalizationStockItemDto[];
  serviceItems?: CreateUpdateAssetCapitalizationServiceItemDto[];
  consumedAssets?: CreateUpdateAssetCapitalizationAssetItemDto[];
}

export interface CreateUpdateAssetCapitalizationServiceItemDto {
  itemId?: string;
  itemName?: string;
  amount?: number;
  expenseAccountId?: string | null;
}

export interface CreateUpdateAssetCapitalizationStockItemDto {
  itemId?: string;
  itemName?: string;
  qty?: number;
  rate?: number;
  warehouseId?: string | null;
}

export interface CreateUpdateAssetCategoryAccountDto {
  id?: string | null;
  companyId?: string;
  fixedAssetAccountId?: string;
  accumulatedDepreciationAccountId?: string | null;
  depreciationExpenseAccountId?: string | null;
  capitalWorkInProgressAccountId?: string | null;
}

export interface CreateUpdateAssetCategoryDto {
  categoryName: string;
  isDepreciable?: boolean;
  enableCwipAccounting?: boolean;
  nonDepreciableCategory?: boolean;
  defaultDepreciationMethod?: DepreciationMethod;
  defaultUsefulLifeMonths?: number;
  defaultDepreciationRate?: number | null;
  defaultFrequencyMonths?: number;
  assetAccountId?: string | null;
  depreciationAccountId?: string | null;
  accumulatedDepreciationAccountId?: string | null;
  accounts?: CreateUpdateAssetCategoryAccountDto[];
}

export interface CreateUpdateAssetMaintenanceDto {
  companyId?: string;
  assetId?: string;
  assetName?: string | null;
  itemId?: string | null;
  itemCode?: string | null;
  itemName?: string | null;
  maintenanceManagerId?: string | null;
  maintenanceManagerName?: string | null;
  maintenanceTeamId?: string | null;
  maintenanceTeamName?: string | null;
  tasks?: CreateUpdateAssetMaintenanceTaskDto[];
}

export interface CreateUpdateAssetMaintenanceLogDto {
  companyId?: string;
  assetMaintenanceId?: string;
  assetMaintenanceTaskId?: string;
  assetId?: string;
  assetName?: string | null;
  itemId?: string | null;
  itemCode?: string | null;
  itemName?: string | null;
  maintenanceTask?: string;
  periodicity?: MaintenancePeriodicity;
  maintenanceType?: string | null;
  dueDate?: string;
  assignToEmployeeId?: string | null;
  assignTo?: string | null;
  assignToName?: string | null;
  hasCertificate?: boolean;
  certificateDetails?: string | null;
  certificateNo?: string | null;
  cost?: number | null;
  description?: string | null;
  remarks?: string | null;
}

export interface CreateUpdateAssetMaintenanceTaskDto {
  id?: string | null;
  maintenanceTask?: string;
  periodicity?: MaintenancePeriodicity;
  maintenanceType?: string | null;
  startDate?: string;
  endDate?: string | null;
  nextDueDate?: string | null;
  assignToEmployeeId?: string | null;
  assignTo?: string | null;
  assignToName?: string | null;
  certificateRequired?: boolean;
  description?: string | null;
  certificateNo?: string | null;
}

export interface CreateUpdateAssetMaintenanceTeamDto {
  companyId?: string;
  teamName?: string;
  maintenanceManagerId?: string | null;
  members?: AssetMaintenanceTeamMemberDto[];
}

export interface CreateUpdateAssetMovementDto {
  companyId?: string;
  purpose?: AssetMovementPurpose;
  transactionDate?: string;
  referenceType?: string | null;
  referenceId?: string | null;
  assetId?: string | null;
  sourceLocation?: string | null;
  sourceLocationId?: string | null;
  sourceEmployeeId?: string | null;
  targetLocation?: string | null;
  targetLocationId?: string | null;
  targetEmployeeId?: string | null;
  items?: CreateUpdateAssetMovementItemDto[];
}

export interface CreateUpdateAssetMovementItemDto {
  id?: string | null;
  assetId?: string;
  assetName?: string | null;
  sourceLocation?: string | null;
  sourceLocationId?: string | null;
  targetLocation?: string | null;
  targetLocationId?: string | null;
  fromEmployeeId?: string | null;
  toEmployeeId?: string | null;
}

export interface CreateUpdateAssetRepairConsumedItemDto {
  id?: string | null;
  itemId?: string;
  itemName?: string | null;
  warehouseId?: string | null;
  qty?: number;
  valuationRate?: number;
  serialAndBatchBundleId?: string | null;
}

export interface CreateUpdateAssetRepairDto {
  companyId?: string;
  assetId?: string;
  repairDescription?: string | null;
  actionsPerformed?: string | null;
  downtime?: string | null;
  failureDate?: string | null;
  completionDate?: string | null;
  costCenterId?: string | null;
  projectId?: string | null;
  repairCost?: number;
  capitalizeRepairCost?: boolean;
  increaseInAssetLife?: number;
  stockItems?: CreateUpdateAssetRepairConsumedItemDto[];
  invoices?: CreateUpdateAssetRepairPurchaseInvoiceDto[];
}

export interface CreateUpdateAssetRepairPurchaseInvoiceDto {
  id?: string | null;
  purchaseInvoiceId?: string;
  purchaseInvoiceNumber?: string | null;
  repairCost?: number;
  expenseAccountId?: string | null;
}

export interface CreateUpdateAssetShiftFactorDto {
  shiftName: string;
  factor?: number;
  isDefault?: boolean;
}

export interface CreateUpdateAssetValueAdjustmentDto {
  companyId?: string;
  assetId?: string;
  financeBookId?: string | null;
  date?: string;
  currentAssetValue?: number;
  newAssetValue?: number;
  differenceAccountId?: string;
  costCenterId?: string | null;
  notes?: string | null;
}

export interface CreateUpdateDriverDto {
  companyId: string;
  fullName: string;
  employeeId?: string | null;
  transporterId?: string | null;
  cellNumber?: string | null;
  licenseNumber: string;
  licenseExpiryDate?: string | null;
  address?: string | null;
  licenseCategoryIds?: string[];
}

export interface CreateUpdateDrivingLicenseCategoryDto {
  categoryName: string;
  description?: string | null;
}

export interface CreateUpdateLocationDto {
  locationName: string;
  parentLocationId?: string | null;
  isContainer?: boolean;
  isGroup?: boolean;
  latitude?: number | null;
  longitude?: number | null;
}

export interface CreateUpdateVehicleDto {
  companyId: string;
  licensePlate: string;
  make?: string | null;
  model?: string | null;
  chassisNumber?: string | null;
  color?: string | null;
  fuelType?: VehicleFuelType;
  fuelUom?: string | null;
  lastOdometer?: number;
  carryingCapacity?: number | null;
  wheels?: number | null;
  doors?: number | null;
  vehicleValue?: number | null;
  acquisitionDate?: string | null;
  driverId?: string | null;
  locationId?: string | null;
  insuranceCompany?: string | null;
  policyNumber?: string | null;
  insuranceStartDate?: string | null;
  insuranceEndDate?: string | null;
  roadTaxExpiryDate?: string | null;
  fitnessCertificateExpiryDate?: string | null;
}

export interface DepreciationScheduleDto extends EntityDto<string> {
  scheduleDate?: string;
  depreciationAmount?: number;
  accumulatedDepreciation?: number;
  isBooked?: boolean;
  shiftFactorId?: string | null;
}

export interface DriverDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  fullName?: string;
  employeeId?: string | null;
  transporterId?: string | null;
  cellNumber?: string | null;
  licenseNumber?: string;
  licenseExpiryDate?: string | null;
  address?: string | null;
  status?: DriverStatus;
  licenseCategoryIds?: string[];
}

export interface DrivingLicenseCategoryDto extends FullAuditedEntityDto<string> {
  categoryName?: string;
  description?: string | null;
}

export interface GetAssetListDto extends PagedAndSortedResultRequestDto {
  status?: AssetStatus | null;
  filter?: string | null;
  companyId?: string | null;
  assetCategoryId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface GetAssetMaintenanceListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  assetId?: string | null;
  filter?: string | null;
}

export interface GetAssetMaintenanceLogListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  assetId?: string | null;
  assetMaintenanceId?: string | null;
  status?: AssetMaintenanceStatus | null;
  filter?: string | null;
}

export interface GetMaintenanceVisitListDto extends PagedAndSortedResultRequestDto {
  completionStatus?: MaintenanceVisitStatus | null;
  maintenanceScheduleId?: string | null;
  maintenanceType?: string | null;
  customerId?: string | null;
}

export interface LocationDto extends FullAuditedEntityDto<string> {
  locationName?: string;
  parentLocationId?: string | null;
  parentLocationName?: string | null;
  isContainer?: boolean;
  isGroup?: boolean;
  latitude?: number | null;
  longitude?: number | null;
}

export interface MaintenanceScheduleDetailDto {
  id?: string;
  scheduledDate?: string;
  actualDate?: string | null;
  isCompleted?: boolean;
}

export interface MaintenanceScheduleDto extends EntityDto<string> {
  companyId?: string;
  assetId?: string | null;
  itemId?: string | null;
  customerId?: string | null;
  startDate?: string;
  endDate?: string;
  periodicity?: string;
  status?: number;
  details?: MaintenanceScheduleDetailDto[];
}

export interface MaintenanceVisitDto extends EntityDto<string> {
  companyId?: string;
  visitDate?: string;
  maintenanceType?: string;
  maintenanceScheduleId?: string | null;
  customerId?: string | null;
  contactId?: string | null;
  completionStatus?: MaintenanceVisitStatus;
  purposes?: MaintenanceVisitPurposeDto[];
  creationTime?: string;
}

export interface MaintenanceVisitPurposeDto {
  id?: string;
  itemId?: string | null;
  itemName?: string | null;
  serialNoId?: string | null;
  workDone?: string;
  workDetails?: string | null;
}

export interface UpdateAssetDto {
  assetName: string;
  assetCategoryId?: string | null;
  itemId?: string | null;
  location?: string | null;
  locationId?: string | null;
  custodianEmployeeId?: string | null;
  additionalCost?: number;
  calculateDepreciation?: boolean;
  depreciationMethod?: DepreciationMethod;
  usefulLifeMonths?: number;
  depreciationRate?: number;
  frequencyMonths?: number;
  availableForUseDate?: string | null;
  openingAccumulatedDepreciation?: number;
  notes?: string | null;
}

export interface VehicleDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  licensePlate?: string;
  make?: string | null;
  model?: string | null;
  chassisNumber?: string | null;
  color?: string | null;
  fuelType?: VehicleFuelType;
  fuelUom?: string | null;
  lastOdometer?: number;
  carryingCapacity?: number | null;
  wheels?: number | null;
  doors?: number | null;
  vehicleValue?: number | null;
  acquisitionDate?: string | null;
  driverId?: string | null;
  driverName?: string | null;
  locationId?: string | null;
  insuranceCompany?: string | null;
  policyNumber?: string | null;
  insuranceStartDate?: string | null;
  insuranceEndDate?: string | null;
  roadTaxExpiryDate?: string | null;
  fitnessCertificateExpiryDate?: string | null;
  isDisabled?: boolean;
}
