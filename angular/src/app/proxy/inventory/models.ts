import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StockEntryType } from './stock-entry-type.enum';
import type { ItemType } from './item-type.enum';
import type { ValuationMethod } from './valuation-method.enum';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

export interface BatchDto extends AuditedEntityDto<string> {
  batchNo?: string;
  itemId?: string;
  manufacturingDate?: string | null;
  expiryDate?: string | null;
  shelfLifeInDays?: number | null;
  supplierBatchNo?: string | null;
  isDisabled?: boolean;
  isExpired?: boolean;
  description?: string | null;
}

export interface CreateBatchDto {
  itemId: string;
  batchNo: string;
  manufacturingDate?: string | null;
  expiryDate?: string | null;
  shelfLifeInDays?: number | null;
  supplierBatchNo?: string | null;
  description?: string | null;
}

export interface CreateItemAttributeDto {
  name?: string;
  isNumeric?: boolean;
  fromRange?: number;
  toRange?: number;
  increment?: number;
  values?: ItemAttributeValueDto[];
}

export interface CreateItemGroupDto {
  name?: string;
  parentId?: string | null;
  isGroup?: boolean;
  defaultWarehouseId?: string | null;
}

export interface CreatePickListDto {
  companyId?: string;
  purpose?: string;
  salesOrderId?: string | null;
  materialRequestId?: string | null;
  workOrderId?: string | null;
  items?: CreatePickListItemDto[];
}

export interface CreatePickListItemDto {
  itemId?: string;
  itemName?: string | null;
  warehouseId?: string;
  qty?: number;
  batchId?: string | null;
}

export interface CreateStockClosingDto {
  companyId?: string;
  toDate?: string;
}

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

export interface CreateStockReservationDto {
  companyId?: string;
  itemId?: string;
  warehouseId?: string;
  voucherType?: string;
  voucherId?: string;
  voucherDetailId?: string | null;
  reservedQty?: number;
  batchId?: string | null;
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
  reorderLevel?: number;
  reorderQty?: number;
  safetyStock?: number;
  defaultWarehouseId?: string | null;
  minOrderQty?: number;
  inspectionRequiredBeforePurchase?: boolean;
  inspectionRequiredBeforeDelivery?: boolean;
}

export interface CreateUpdateItemPriceDto {
  itemId: string;
  priceListId: string;
  priceListRate: number;
  uom?: string;
  currencyCode?: string;
  minQty?: number;
  validFrom?: string | null;
  validUpto?: string | null;
  customerId?: string | null;
  supplierId?: string | null;
  batchNo?: string | null;
}

export interface CreateUpdatePriceListDto {
  name: string;
  currencyCode: string;
  isSelling?: boolean;
  isBuying?: boolean;
  isDefault?: boolean;
  companyId?: string | null;
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

export interface GetBatchListDto extends PagedAndSortedResultRequestDto {
  itemId?: string | null;
  isDisabled?: boolean | null;
  filter?: string | null;
}

export interface GetItemListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  companyId?: string | null;
}

export interface GetItemPriceListDto extends PagedAndSortedResultRequestDto {
  itemId?: string | null;
  priceListId?: string | null;
  customerId?: string | null;
  supplierId?: string | null;
}

export interface GetItemRateRequestDto {
  itemId: string;
  priceListId: string;
  qty?: number;
  transactionDate?: string | null;
  customerId?: string | null;
  supplierId?: string | null;
  batchNo?: string | null;
}

export interface GetSerialNoListDto extends PagedAndSortedResultRequestDto {
  itemId?: string | null;
  warehouseId?: string | null;
  filter?: string | null;
}

export interface GetStockBalanceRequestDto extends PagedAndSortedResultRequestDto {
  itemId?: string | null;
  warehouseId?: string | null;
}

export interface GetStockReservationListDto extends CompanyFilteredPagedRequestDto {
  itemId?: string | null;
  warehouseId?: string | null;
  voucherId?: string | null;
  status?: string | null;
}

export interface ItemAttributeDto {
  id?: string;
  name?: string;
  isNumeric?: boolean;
  fromRange?: number;
  toRange?: number;
  increment?: number;
  values?: ItemAttributeValueDto[];
}

export interface ItemAttributeValueDto {
  value?: string;
  abbreviation?: string;
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
  reorderLevel?: number;
  reorderQty?: number;
  safetyStock?: number;
  defaultWarehouseId?: string | null;
  minOrderQty?: number;
  inspectionRequiredBeforePurchase?: boolean;
  inspectionRequiredBeforeDelivery?: boolean;
}

export interface ItemGroupDto extends EntityDto<string> {
  name?: string;
  parentId?: string | null;
  isGroup?: boolean;
  defaultWarehouseId?: string | null;
}

export interface ItemPriceDto extends AuditedEntityDto<string> {
  itemId?: string;
  itemName?: string | null;
  priceListId?: string;
  priceListName?: string | null;
  priceListRate?: number;
  uom?: string;
  currencyCode?: string;
  minQty?: number;
  validFrom?: string | null;
  validUpto?: string | null;
  customerId?: string | null;
  supplierId?: string | null;
  batchNo?: string | null;
}

export interface ItemRateResultDto {
  rate?: number;
  itemPriceId?: string | null;
  source?: string | null;
}

export interface ManufactureItemLineDto {
  itemId?: string;
  itemName?: string;
  requiredQty?: number;
  rate?: number;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  isRawMaterial?: boolean;
}

export interface ManufactureItemsDto {
  workOrderId?: string;
  bomId?: string;
  produceQty?: number;
  fgItemId?: string;
  fgWarehouseId?: string | null;
  sourceWarehouseId?: string | null;
  items?: ManufactureItemLineDto[];
}

export interface PendingTransferDto {
  pickListItemId?: string;
  itemId?: string;
  warehouseId?: string;
  pendingQty?: number;
  batchId?: string | null;
}

export interface PickAllocationDto {
  itemId?: string;
  warehouseId?: string;
  requestedQty?: number;
  allocatedQty?: number;
  shortageQty?: number;
}

export interface PickAllocationResultDto {
  hasShortage?: boolean;
  allocations?: PickAllocationDto[];
}

export interface PickListDto extends EntityDto<string> {
  companyId?: string;
  pickListNumber?: string | null;
  purpose?: string;
  salesOrderId?: string | null;
  status?: number;
  isFullyTransferred?: boolean;
  isPartiallyTransferred?: boolean;
  items?: PickListItemDto[];
  creationTime?: string;
}

export interface PickListItemDto {
  id?: string;
  itemId?: string;
  itemName?: string | null;
  warehouseId?: string;
  qty?: number;
  transferredQty?: number;
  pendingQty?: number;
}

export interface PriceListDto extends AuditedEntityDto<string> {
  name?: string;
  currencyCode?: string;
  isSelling?: boolean;
  isBuying?: boolean;
  isDefault?: boolean;
  isActive?: boolean;
  companyId?: string | null;
}

export interface SerialNoDto extends EntityDto<string> {
  serialNumber?: string;
  itemId?: string;
  warehouseId?: string | null;
  companyId?: string;
  batchId?: string | null;
  customerId?: string | null;
  purchaseRate?: number;
  warrantyExpiryDate?: string | null;
  amcExpiryDate?: string | null;
  maintenanceStatus?: string;
  status?: number;
  creationTime?: string;
}

export interface StockBalanceDto {
  id?: string;
  itemId?: string;
  warehouseId?: string;
  actualQty?: number;
  orderedQty?: number;
  plannedQty?: number;
  reservedQty?: number;
  indentedQty?: number;
  projectedQty?: number;
  stockValue?: number;
  valuationRate?: number;
}

export interface StockClosingEntryDto extends EntityDto<string> {
  companyId?: string;
  toDate?: string;
  status?: number;
  totalEntries?: number;
  totalStockValue?: number;
  previousClosingEntryId?: string | null;
  scannedFromDate?: string | null;
  creationTime?: string;
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

export interface StockReservationEntryDto extends EntityDto<string> {
  companyId?: string;
  itemId?: string;
  warehouseId?: string;
  voucherType?: string;
  voucherId?: string;
  voucherDetailId?: string | null;
  reservedQty?: number;
  deliveredQty?: number;
  availableQty?: number;
  status?: number;
  creationTime?: string;
}

export interface StockValuationRowDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  uom?: string;
  warehouseId?: string;
  warehouseName?: string;
  quantity?: number;
  valuationRate?: number;
  stockValue?: number;
}

export interface StockValuationSummaryDto {
  companyId?: string;
  totalStockValue?: number;
  totalItems?: number;
  totalWarehouses?: number;
  rows?: StockValuationRowDto[];
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
