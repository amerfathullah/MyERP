import type { AuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { DepreciationMethod } from './depreciation-method.enum';
import type { AssetStatus } from './asset-status.enum';

export interface AssetCapitalizationDto {
  id?: string;
  companyId?: string;
  targetAssetName?: string | null;
  targetAssetId?: string;
  postingDate?: string;
  totalAssetValue?: number;
  status?: string;
}

export interface AssetCategoryDetailDto extends EntityDto<string> {
  categoryName?: string;
  isDepreciable?: boolean;
  defaultDepreciationMethod?: DepreciationMethod;
  defaultUsefulLifeMonths?: number;
  defaultDepreciationRate?: number | null;
  assetAccountId?: string | null;
  depreciationAccountId?: string | null;
  accumulatedDepreciationAccountId?: string | null;
}

export interface AssetCategoryDto {
  id?: string;
  categoryName?: string;
  isDepreciable?: boolean;
  defaultDepreciationMethod?: DepreciationMethod;
  defaultUsefulLifeMonths?: number;
}

export interface AssetDto extends AuditedEntityDto<string> {
  assetNumber?: string;
  assetName?: string;
  status?: AssetStatus;
  companyId?: string;
  assetCategoryId?: string | null;
  location?: string | null;
  purchaseDate?: string;
  purchaseAmount?: number;
  additionalCost?: number;
  totalAssetCost?: number;
  calculateDepreciation?: boolean;
  depreciationMethod?: DepreciationMethod;
  usefulLifeMonths?: number;
  valueAfterDepreciation?: number;
  isFullyDepreciated?: boolean;
  disposalDate?: string | null;
  disposalAmount?: number | null;
  notes?: string | null;
  schedule?: DepreciationScheduleDto[];
}

export interface AssetMovementDto extends EntityDto<string> {
  companyId?: string;
  assetId?: string;
  movementType?: string;
  movementDate?: string;
  sourceLocation?: string | null;
  targetLocation?: string | null;
  purpose?: string | null;
  status?: number;
}

export interface AssetRepairDto extends EntityDto<string> {
  companyId?: string;
  assetId?: string;
  repairDescription?: string | null;
  failureDate?: string | null;
  completionDate?: string | null;
  repairCost?: number;
  capitalizeRepairCost?: boolean;
  increaseInAssetLife?: number;
  stockItemConsumedCost?: number;
  status?: number;
  creationTime?: string;
}

export interface CapConsumedAssetDto {
  assetId?: string;
  assetName?: string;
  valueAfterDepreciation?: number;
}

export interface CapServiceItemDto {
  itemId?: string;
  itemName?: string;
  amount?: number;
  expenseAccountId?: string | null;
}

export interface CapStockItemDto {
  itemId?: string;
  itemName?: string;
  quantity?: number;
  rate?: number;
  warehouseId?: string | null;
}

export interface CreateAssetCapitalizationDto {
  companyId?: string;
  capitalizationNumber?: string;
  targetAssetName?: string | null;
  targetAssetId?: string;
  postingDate?: string;
  stockItems?: CapStockItemDto[];
  serviceItems?: CapServiceItemDto[];
  consumedAssets?: CapConsumedAssetDto[];
}

export interface CreateAssetDto {
  assetName: string;
  companyId: string;
  assetCategoryId?: string | null;
  location?: string | null;
  purchaseDate: string;
  purchaseAmount?: number;
  additionalCost?: number;
  calculateDepreciation?: boolean;
  depreciationMethod?: DepreciationMethod;
  usefulLifeMonths?: number;
  depreciationRate?: number;
  frequencyMonths?: number;
  availableForUseDate?: string | null;
  notes?: string | null;
}

export interface CreateAssetMovementDto {
  companyId?: string;
  assetId?: string;
  movementType?: string;
  movementDate?: string;
  sourceLocation?: string | null;
  sourceEmployeeId?: string | null;
  targetLocation?: string | null;
  targetEmployeeId?: string | null;
  purpose?: string | null;
}

export interface CreateAssetRepairDto {
  companyId?: string;
  assetId?: string;
  repairDescription?: string | null;
  failureDate?: string | null;
  repairCost?: number;
  capitalizeRepairCost?: boolean;
  increaseInAssetLife?: number;
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

export interface CreateUpdateAssetCategoryDetailDto {
  categoryName?: string;
  isDepreciable?: boolean;
  defaultDepreciationMethod?: DepreciationMethod;
  defaultUsefulLifeMonths?: number;
  defaultDepreciationRate?: number | null;
  assetAccountId?: string | null;
  depreciationAccountId?: string | null;
  accumulatedDepreciationAccountId?: string | null;
}

export interface CreateUpdateAssetCategoryDto {
  categoryName: string;
  isDepreciable?: boolean;
  defaultDepreciationMethod?: DepreciationMethod;
  defaultUsefulLifeMonths?: number;
  defaultDepreciationRate?: number | null;
}

export interface DepreciationScheduleDto {
  id?: string;
  scheduleDate?: string;
  depreciationAmount?: number;
  accumulatedDepreciation?: number;
  isBooked?: boolean;
}

export interface GetAssetListDto extends PagedAndSortedResultRequestDto {
  status?: AssetStatus | null;
  filter?: string | null;
  companyId?: string | null;
  assetCategoryId?: string | null;
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

export interface UpdateAssetDto {
  assetName: string;
  assetCategoryId?: string | null;
  location?: string | null;
  additionalCost?: number;
  notes?: string | null;
}
