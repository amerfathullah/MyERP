import type { StockEntryType } from './stock-entry-type.enum';
import type { ItemType } from './item-type.enum';
import type { ValuationMethod } from './valuation-method.enum';
import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateStockEntryDto {
  companyId: string;
  entryType: StockEntryType;
  postingDate: string;
  referenceType?: string | null;
  referenceId?: string | null;
  notes?: string | null;
  items: CreateStockEntryItemDto[];
}

export interface CreateStockEntryItemDto {
  itemId: string;
  quantity: number;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  valuationRate?: number | null;
}

export interface CreateUpdateItemDto {
  companyId: string;
  itemCode: string;
  itemName: string;
  barcode?: string | null;
  description?: string | null;
  itemType: ItemType;
  itemGroup?: string | null;
  brand?: string | null;
  uom: string;
  valuationMethod?: ValuationMethod;
  standardSellingPrice?: number | null;
  standardBuyingPrice?: number | null;
  taxCategoryId?: string | null;
  maintainStock?: boolean;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  isActive?: boolean;
}

export interface CreateUpdateWarehouseDto {
  companyId: string;
  branchId?: string | null;
  name: string;
  warehouseCode?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  parentWarehouseId?: string | null;
  isGroup?: boolean;
  isActive?: boolean;
}

export interface ItemDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  itemCode?: string;
  itemName?: string;
  barcode?: string | null;
  description?: string | null;
  itemType?: ItemType;
  itemGroup?: string | null;
  brand?: string | null;
  uom?: string;
  valuationMethod?: ValuationMethod;
  standardSellingPrice?: number | null;
  standardBuyingPrice?: number | null;
  taxCategoryId?: string | null;
  maintainStock?: boolean;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  isActive?: boolean;
}

export interface StockEntryDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  entryNumber?: string | null;
  entryType?: StockEntryType;
  postingDate?: string;
  referenceType?: string | null;
  referenceId?: string | null;
  notes?: string | null;
  status?: string;
  items?: StockEntryItemDto[];
}

export interface StockEntryItemDto {
  id?: string;
  itemId?: string;
  quantity?: number;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  valuationRate?: number | null;
}

export interface StockLedgerReportDto {
  fromDate?: string;
  toDate?: string;
  rows?: StockLedgerRowDto[];
  totalIn?: number;
  totalOut?: number;
}

export interface StockLedgerRequestDto {
  companyId: string;
  fromDate: string;
  toDate: string;
  itemId?: string | null;
  warehouseId?: string | null;
}

export interface StockLedgerRowDto {
  postingDate?: string;
  itemName?: string;
  warehouseName?: string;
  quantityChange?: number;
  valuationRate?: number;
  stockValue?: number;
  balanceQuantity?: number;
  balanceValue?: number;
  voucherType?: string | null;
  voucherId?: string | null;
}

export interface WarehouseDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  branchId?: string | null;
  name?: string;
  warehouseCode?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  parentWarehouseId?: string | null;
  isGroup?: boolean;
  isActive?: boolean;
}

// Quality Inspection
export interface QualityInspectionDto {
  id?: string;
  companyId?: string;
  itemId?: string;
  itemName?: string;
  inspectionType?: number;
  referenceType?: string;
  referenceId?: string;
  batchNo?: string;
  sampleSize?: number;
  inspectionDate?: string;
  status?: number;
  docStatus?: number;
  remarks?: string;
  manualInspection?: boolean;
  readings?: QualityInspectionReadingDto[];
  creationTime?: string;
}

export interface QualityInspectionReadingDto {
  id?: string;
  specification?: string;
  expectedValue?: string;
  minValue?: number;
  maxValue?: number;
  readingValue?: string;
  isNumeric?: boolean;
  formulaBased?: boolean;
  status?: number;
}

export interface CreateQualityInspectionDto {
  companyId: string;
  itemId: string;
  itemName?: string;
  inspectionType: number;
  inspectionDate: string;
  referenceType?: string;
  referenceId?: string;
  batchNo?: string;
  sampleSize: number;
  manualInspection?: boolean;
  readings: CreateQualityInspectionReadingDto[];
}

export interface CreateQualityInspectionReadingDto {
  specification: string;
  expectedValue?: string;
  minValue?: number;
  maxValue?: number;
  readingValue?: string;
  isNumeric?: boolean;
  formulaBased?: boolean;
  formula?: string;
}

export interface GetQualityInspectionListDto {
  companyId?: string;
  itemId?: string;
  status?: number;
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

// Stock Reconciliation
export interface StockReconciliationDto {
  id?: string;
  companyId?: string;
  reconciliationNumber?: string;
  postingDate?: string;
  purpose?: string;
  notes?: string;
  status?: number;
  differenceAmount?: number;
  items?: StockReconciliationItemDto[];
  creationTime?: string;
}

export interface StockReconciliationItemDto {
  id?: string;
  itemId?: string;
  warehouseId?: string;
  currentQuantity?: number;
  currentValuationRate?: number;
  newQuantity?: number;
  newValuationRate?: number;
  quantityDifference?: number;
  differenceAmount?: number;
}

export interface CreateStockReconciliationDto {
  companyId: string;
  postingDate: string;
  purpose?: string;
  notes?: string;
  expenseAccountId?: string;
  costCenterId?: string;
  items: CreateStockReconciliationItemDto[];
}

export interface CreateStockReconciliationItemDto {
  itemId: string;
  warehouseId: string;
  newQuantity: number;
  newValuationRate: number;
  currentQuantity: number;
  currentValuationRate: number;
}

export interface GetStockReconciliationListDto {
  companyId?: string;
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

// Landed Cost Voucher
export interface LandedCostVoucherDto {
  id?: string;
  companyId?: string;
  voucherNumber?: string;
  postingDate?: string;
  distributionMethod?: number;
  status?: number;
  totalCharges?: number;
  totalDistributedAmount?: number;
  notes?: string;
  items?: LandedCostItemDto[];
  charges?: LandedCostChargeDto[];
  creationTime?: string;
}

export interface LandedCostItemDto {
  id?: string;
  receiptId?: string;
  receiptType?: string;
  itemId?: string;
  description?: string;
  quantity?: number;
  amount?: number;
  applicableCharges?: number;
}

export interface LandedCostChargeDto {
  id?: string;
  description?: string;
  expenseAccountId?: string;
  amount?: number;
}

export interface CreateLandedCostVoucherDto {
  companyId: string;
  postingDate: string;
  distributionMethod?: number;
  notes?: string;
  items: CreateLandedCostItemDto[];
  charges: CreateLandedCostChargeDto[];
}

export interface CreateLandedCostItemDto {
  receiptId: string;
  receiptType: string;
  itemId: string;
  description?: string;
  quantity: number;
  amount: number;
}

export interface CreateLandedCostChargeDto {
  description: string;
  expenseAccountId: string;
  amount: number;
}

export interface GetLandedCostVoucherListDto {
  companyId?: string;
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}
