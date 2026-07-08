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
