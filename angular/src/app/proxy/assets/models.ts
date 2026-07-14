export interface AssetDto {
  id?: string;
  assetNumber?: string;
  assetName?: string;
  status?: number;
  companyId?: string;
  assetCategoryId?: string;
  location?: string;
  purchaseDate?: string;
  purchaseAmount?: number;
  additionalCost?: number;
  totalAssetCost?: number;
  calculateDepreciation?: boolean;
  depreciationMethod?: number;
  usefulLifeMonths?: number;
  valueAfterDepreciation?: number;
  isFullyDepreciated?: boolean;
  disposalDate?: string;
  disposalAmount?: number;
  notes?: string;
  schedule?: DepreciationScheduleDto[];
  creationTime?: string;
}

export interface DepreciationScheduleDto {
  id?: string;
  scheduleDate?: string;
  depreciationAmount?: number;
  accumulatedDepreciation?: number;
  isBooked?: boolean;
}

export interface CreateAssetDto {
  assetName: string;
  companyId: string;
  assetCategoryId?: string;
  location?: string;
  purchaseDate: string;
  purchaseAmount: number;
  additionalCost?: number;
  calculateDepreciation?: boolean;
  depreciationMethod?: number;
  usefulLifeMonths?: number;
  depreciationRate?: number;
  frequencyMonths?: number;
  availableForUseDate?: string;
  notes?: string;
}

export interface UpdateAssetDto {
  assetName: string;
  assetCategoryId?: string;
  location?: string;
  additionalCost?: number;
  notes?: string;
}

export interface AssetCategoryDto {
  id?: string;
  categoryName?: string;
  isDepreciable?: boolean;
  defaultDepreciationMethod?: number;
  defaultUsefulLifeMonths?: number;
}

export interface CreateUpdateAssetCategoryDto {
  categoryName: string;
  isDepreciable?: boolean;
  defaultDepreciationMethod?: number;
  defaultUsefulLifeMonths?: number;
  defaultDepreciationRate?: number;
}

export interface AssetMovementDto { [key: string]: any; }

export interface CreateAssetMovementDto { [key: string]: any; }

export interface MaintenanceScheduleDto { [key: string]: any; }

export interface CreateMaintenanceScheduleDto { [key: string]: any; }
