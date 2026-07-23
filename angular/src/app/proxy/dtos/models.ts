import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { DocumentStatus } from '../core/document-status.enum';
import type { BudgetAction } from '../accounting/budget-action.enum';
import type { LandedCostDistributionMethod } from '../inventory/landed-cost-distribution-method.enum';
import type { InspectionType } from '../inventory/inspection-type.enum';
import type { InspectionStatus } from '../inventory/inspection-status.enum';

export interface BudgetAccountDto extends EntityDto<string> {
  accountId?: string;
  accountName?: string | null;
  budgetAmount?: number;
}

export interface BudgetDto extends EntityDto<string> {
  companyId?: string;
  fiscalYearId?: string;
  budgetAgainst?: string;
  budgetAgainstId?: string;
  budgetAgainstName?: string | null;
  status?: DocumentStatus;
  actionIfAnnualBudgetExceeded?: BudgetAction;
  actionIfAccumulatedMonthlyBudgetExceeded?: BudgetAction;
  accounts?: BudgetAccountDto[];
  creationTime?: string;
}

export interface CreateBudgetAccountDto {
  accountId?: string;
  accountName?: string | null;
  budgetAmount?: number;
}

export interface CreateBudgetDto {
  companyId?: string;
  fiscalYearId?: string;
  budgetAgainst?: string;
  budgetAgainstId?: string;
  budgetAgainstName?: string | null;
  actionIfAnnualBudgetExceeded?: BudgetAction;
  actionIfAccumulatedMonthlyBudgetExceeded?: BudgetAction;
  accounts?: CreateBudgetAccountDto[];
}

export interface CreateLandedCostChargeDto {
  description?: string;
  expenseAccountId?: string;
  amount?: number;
}

export interface CreateLandedCostItemDto {
  receiptId?: string;
  receiptType?: string;
  itemId?: string;
  description?: string | null;
  quantity?: number;
  amount?: number;
}

export interface CreateLandedCostVoucherDto {
  companyId?: string;
  postingDate?: string;
  distributionMethod?: LandedCostDistributionMethod;
  notes?: string | null;
  items?: CreateLandedCostItemDto[];
  charges?: CreateLandedCostChargeDto[];
}

export interface CreateQualityInspectionDto {
  companyId?: string;
  itemId?: string;
  itemName?: string | null;
  inspectionType?: InspectionType;
  referenceType?: string | null;
  referenceId?: string | null;
  batchNo?: string | null;
  sampleSize?: number;
  inspectionDate?: string;
  manualInspection?: boolean;
  readings?: CreateQualityInspectionReadingDto[];
}

export interface CreateQualityInspectionReadingDto {
  specification?: string;
  expectedValue?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  readingValue?: string | null;
  isNumeric?: boolean;
  formulaBased?: boolean;
  formula?: string | null;
}

export interface CreateStockReconciliationDto {
  companyId?: string;
  postingDate?: string;
  purpose?: string | null;
  notes?: string | null;
  expenseAccountId?: string | null;
  costCenterId?: string | null;
  items?: CreateStockReconciliationItemDto[];
}

export interface CreateStockReconciliationItemDto {
  itemId?: string;
  warehouseId?: string;
  newQuantity?: number;
  newValuationRate?: number;
  currentQuantity?: number;
  currentValuationRate?: number;
}

export interface GetBudgetListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  fiscalYearId?: string | null;
  filter?: string | null;
  status?: string | null;
}

export interface GetLandedCostVoucherListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  filter?: string | null;
}

export interface GetQualityInspectionListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  itemId?: string | null;
  status?: InspectionStatus | null;
  filter?: string | null;
}

export interface GetStockReconciliationListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  filter?: string | null;
}

export interface LandedCostChargeDto extends EntityDto<string> {
  description?: string;
  expenseAccountId?: string;
  amount?: number;
}

export interface LandedCostItemDto extends EntityDto<string> {
  receiptId?: string;
  receiptType?: string;
  itemId?: string;
  description?: string | null;
  quantity?: number;
  amount?: number;
  applicableCharges?: number;
}

export interface LandedCostVoucherDto extends EntityDto<string> {
  companyId?: string;
  voucherNumber?: string | null;
  postingDate?: string;
  distributionMethod?: LandedCostDistributionMethod;
  status?: DocumentStatus;
  totalCharges?: number;
  totalDistributedAmount?: number;
  notes?: string | null;
  items?: LandedCostItemDto[];
  charges?: LandedCostChargeDto[];
  creationTime?: string;
}

export interface QualityInspectionDto extends EntityDto<string> {
  companyId?: string;
  itemId?: string;
  itemName?: string | null;
  inspectionType?: InspectionType;
  referenceType?: string | null;
  referenceId?: string | null;
  batchNo?: string | null;
  sampleSize?: number;
  inspectionDate?: string;
  status?: InspectionStatus;
  docStatus?: DocumentStatus;
  remarks?: string | null;
  manualInspection?: boolean;
  readings?: QualityInspectionReadingDto[];
  creationTime?: string;
}

export interface QualityInspectionReadingDto extends EntityDto<string> {
  specification?: string;
  expectedValue?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  readingValue?: string | null;
  isNumeric?: boolean;
  formulaBased?: boolean;
  status?: InspectionStatus;
}

export interface StockReconciliationDto extends EntityDto<string> {
  companyId?: string;
  reconciliationNumber?: string | null;
  postingDate?: string;
  purpose?: string | null;
  notes?: string | null;
  status?: DocumentStatus;
  differenceAmount?: number;
  items?: StockReconciliationItemDto[];
  creationTime?: string;
}

export interface StockReconciliationItemDto extends EntityDto<string> {
  itemId?: string;
  warehouseId?: string;
  currentQuantity?: number;
  currentValuationRate?: number;
  newQuantity?: number;
  newValuationRate?: number;
  quantityDifference?: number;
  differenceAmount?: number;
}
