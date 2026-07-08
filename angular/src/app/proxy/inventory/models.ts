import type { ItemType } from './item-type.enum';
import type { ValuationMethod } from './valuation-method.enum';
import type { FullAuditedEntityDto } from '@abp/ng.core';

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
