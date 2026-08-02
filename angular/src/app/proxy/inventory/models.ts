import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StockEntryType } from './stock-entry-type.enum';
import type { ItemType } from './item-type.enum';
import type { ValuationMethod } from './valuation-method.enum';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

export interface BarcodeScanResultDto {
  success?: boolean;
  scanType?: number;
  scanTypeName?: string;
  barcode?: string;
  message?: string | null;
  itemId?: string | null;
  itemCode?: string | null;
  itemName?: string | null;
  hasSerialNo?: boolean;
  hasBatchNo?: boolean;
  uom?: string | null;
  maintainStock?: boolean;
  serialNoId?: string | null;
  serialNumber?: string | null;
  batchId?: string | null;
  batchNo?: string | null;
  warehouseId?: string | null;
  warehouseName?: string | null;
  action?: number;
  actionName?: string;
}

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

export interface BatchStockBalanceDto {
  batchId?: string;
  batchNo?: string;
  itemId?: string;
  totalQuantity?: number;
  totalValue?: number;
  warehouseBalances?: BatchWarehouseBalanceDto[];
}

export interface BatchWarehouseBalanceDto {
  warehouseId?: string;
  warehouseName?: string;
  quantity?: number;
  stockValue?: number;
  valuationRate?: number;
}

export interface BatchMovementHistoryDto {
  batchId?: string;
  batchNo?: string;
  entries?: BatchMovementEntryDto[];
}

export interface BatchMovementEntryDto {
  id?: string;
  postingDate?: string;
  warehouseId?: string;
  warehouseName?: string;
  quantityChange?: number;
  valuationRate?: number;
  voucherType?: string | null;
  voucherId?: string | null;
  isInward?: boolean;
}

export interface BatchWiseBalanceReportDto {
  rows?: BatchWiseBalanceRowDto[];
  totalBatches?: number;
  totalQuantity?: number;
  totalStockValue?: number;
  expiredBatchCount?: number;
}

export interface BatchWiseBalanceRowDto {
  itemId?: string;
  itemName?: string;
  batchId?: string;
  batchNo?: string;
  warehouseId?: string;
  warehouseName?: string;
  balance?: number;
  stockValue?: number;
  expiryDate?: string | null;
  isExpired?: boolean;
  isDisabled?: boolean;
}

export interface BundleEntryDto {
  serialNo?: string | null;
  batchNo?: string | null;
  qty?: number;
  rate?: number;
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

export interface CreateItemStandardCostDto {
  companyId?: string;
  itemId?: string;
  standardRate?: number;
  effectiveDate?: string;
}

export interface CreateItemVariantDto {
  attributes?: VariantAttributeDto[];
}

export interface CreatePickListDto {
  companyId?: string;
  purpose?: string;
  salesOrderId?: string | null;
  materialRequestId?: string | null;
  workOrderId?: string | null;
  customerId?: string | null;
  items?: CreatePickListItemDto[];
}

export interface CreatePickListItemDto {
  itemId?: string;
  itemName?: string | null;
  warehouseId?: string;
  qty?: number;
  batchId?: string | null;
}

export interface CreateQiTemplateDto {
  name?: string;
  description?: string | null;
  itemId?: string | null;
  bomId?: string | null;
  parameters?: CreateQiTemplateParameterDto[] | null;
}

export interface CreateQiTemplateParameterDto {
  specification?: string;
  expectedValue?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  isNumeric?: boolean;
  formulaBased?: boolean;
  formula?: string | null;
  acceptanceCriteria?: string | null;
}

export interface CreateRepostItemValuationDto {
  companyId?: string;
  basedOn?: number;
  itemId?: string | null;
  warehouseId?: string | null;
  postingDate?: string;
  repostGlEntries?: boolean;
  voucherType?: string | null;
  voucherId?: string | null;
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

export interface CreateTransitTransferDto {
  companyId?: string;
  sourceWarehouseId?: string;
  destinationWarehouseId?: string;
  postingDate?: string;
  notes?: string | null;
  items?: TransitTransferItemDto[];
}

export interface CreateUomDto {
  uomName?: string;
  mustBeWholeNumber?: boolean;
  category?: string | null;
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

export interface CreateUpdatePutawayRuleDto {
  companyId?: string;
  itemId?: string | null;
  itemGroupId?: string | null;
  warehouseId?: string;
  stockCapacity?: number;
  priority?: number;
  uom?: string | null;
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

export interface CreateWarehouseAccountDto {
  warehouseId: string;
  companyId: string;
  accountId: string;
  stockReceivedButNotBilledAccountId?: string | null;
  stockDeliveredButNotBilledAccountId?: string | null;
  stockAdjustmentAccountId?: string | null;
}

export interface GetBatchListDto extends PagedAndSortedResultRequestDto {
  itemId?: string | null;
  isDisabled?: boolean | null;
  filter?: string | null;
}

export interface GetBatchWiseBalanceRequestDto {
  itemId?: string | null;
  warehouseId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  includeZeroBalance?: boolean;
}

export interface GetBundleListDto extends CompanyFilteredPagedRequestDto {
  itemId?: string | null;
  warehouseId?: string | null;
  voucherType?: string | null;
}

export interface GetItemDetailsInput {
  itemId?: string;
  transactionType?: string;
  warehouseId?: string | null;
  companyId?: string | null;
  supplierId?: string | null;
  customerId?: string | null;
  priceListId?: string | null;
  transactionDate?: string | null;
}

export interface GetItemListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  companyId?: string | null;
  itemType?: string | null;
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

export interface GetItemStandardCostListDto extends CompanyFilteredPagedRequestDto {
  itemId?: string | null;
}

export interface GetItemsAvailabilityInput {
  itemIds?: string[];
  warehouseId?: string | null;
}

export interface GetSerialNoListDto extends PagedAndSortedResultRequestDto {
  itemId?: string | null;
  warehouseId?: string | null;
  filter?: string | null;
}

export interface GetStockBalanceRequestDto extends PagedAndSortedResultRequestDto {
  itemId?: string | null;
  warehouseId?: string | null;
  excludeZeroStock?: boolean;
}

export interface GetStockReservationListDto extends CompanyFilteredPagedRequestDto {
  itemId?: string | null;
  warehouseId?: string | null;
  voucherId?: string | null;
  status?: string | null;
}

export interface InventoryAgingBucketDto {
  label?: string;
  itemCount?: number;
  stockValue?: number;
  percentage?: number;
}

export interface InventoryAgingItemDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  warehouseId?: string;
  warehouseName?: string;
  quantity?: number;
  valuationRate?: number;
  stockValue?: number;
  lastMovementDate?: string | null;
  ageDays?: number;
  ageBucket?: string;
}

export interface InventoryAgingReportDto {
  asOfDate?: string;
  totalItems?: number;
  totalStockValue?: number;
  slowMovingValue?: number;
  deadStockValue?: number;
  slowMovingCount?: number;
  deadStockCount?: number;
  buckets?: InventoryAgingBucketDto[];
  items?: InventoryAgingItemDto[];
}

export interface InventoryAgingRequestDto {
  companyId?: string;
  slowMovingDays?: number;
  deadStockDays?: number;
}

export interface InventoryTurnoverItemDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  consumedQty?: number;
  consumedValue?: number;
  currentStockQty?: number;
  currentStockValue?: number;
  turnoverRatio?: number;
  daysToSell?: number;
  category?: string;
}

export interface InventoryTurnoverReportDto {
  fromDate?: string;
  toDate?: string;
  periodDays?: number;
  totalItems?: number;
  fastMovingCount?: number;
  slowMovingCount?: number;
  deadStockCount?: number;
  totalStockValue?: number;
  totalConsumedValue?: number;
  items?: InventoryTurnoverItemDto[];
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

export interface ItemAvailabilityDto {
  itemId?: string;
  actualQty?: number;
  reservedQty?: number;
  orderedQty?: number;
  projectedQty?: number;
  availableQty?: number;
}

export interface ItemDetailsDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  description?: string | null;
  uom?: string;
  stockUom?: string;
  conversionFactor?: number;
  isStockItem?: boolean;
  hasBatchNo?: boolean;
  hasSerialNo?: boolean;
  itemGroup?: string | null;
  rate?: number;
  warehouseId?: string | null;
  incomeAccountId?: string | null;
  expenseAccountId?: string | null;
  actualQty?: number;
  projectedQty?: number;
  reservedQty?: number;
  availableQty?: number;
  companyTotalStock?: number;
  lastPurchaseRate?: number;
  minOrderQty?: number;
  defaultSupplierId?: string | null;
  defaultDiscountPercentage?: number;
  valuationRate?: number;
  blanketOrderId?: string | null;
  blanketOrderNumber?: string | null;
  blanketOrderRate?: number | null;
  blanketOrderRemainingQty?: number | null;
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
  totalStockQty?: number;
  isLowStock?: boolean;
}

export interface ItemGroupDto extends EntityDto<string> {
  name?: string;
  parentId?: string | null;
  isGroup?: boolean;
  defaultWarehouseId?: string | null;
}

export interface ItemMovementEntryDto {
  postingDate?: string;
  warehouseName?: string;
  quantityChange?: number;
  balanceQuantity?: number;
  valuationRate?: number;
  voucherType?: string;
  voucherId?: string | null;
  isInward?: boolean;
}

export interface ItemMovementHistoryDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  totalInward?: number;
  totalOutward?: number;
  currentBalance?: number;
  entries?: ItemMovementEntryDto[];
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

export interface ItemPriceHistoryDto {
  id?: string;
  priceListName?: string | null;
  rate?: number;
  currency?: string;
  validFrom?: string | null;
  validUpto?: string | null;
  isSelling?: boolean;
  isBuying?: boolean;
  partyName?: string | null;
  uom?: string | null;
  createdAt?: string;
}

export interface ItemRateResultDto {
  rate?: number;
  itemPriceId?: string | null;
  source?: string | null;
}

export interface ItemStandardCostDto extends EntityDto<string> {
  companyId?: string;
  itemId?: string;
  standardRate?: number;
  effectiveDate?: string;
  previousRate?: number | null;
  status?: number;
  revaluationStockReconciliationId?: string | null;
  creationTime?: string;
}

export interface ItemStockMovementDto {
  postingDate?: string;
  warehouseName?: string;
  quantityChange?: number;
  valuationRate?: number;
  balanceQty?: number;
  balanceValue?: number;
  voucherType?: string;
  voucherId?: string | null;
}

export interface ItemTransactionSummaryDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  purchaseOrderCount?: number;
  totalPurchasedQty?: number;
  totalPurchasedValue?: number;
  lastPurchaseRate?: number | null;
  lastPurchaseDate?: string | null;
  salesOrderCount?: number;
  totalSoldQty?: number;
  totalSoldValue?: number;
  averageSellingRate?: number | null;
  lastSaleDate?: string | null;
  currentStock?: number;
  reorderLevel?: number;
  isLowStock?: boolean;
  daysOfStockRemaining?: number;
}

export interface ItemVariantDto {
  id?: string;
  itemCode?: string;
  itemName?: string;
  isActive?: boolean;
  standardSellingPrice?: number | null;
  standardBuyingPrice?: number | null;
}

export interface ItemWhereUsedDto {
  bomId?: string;
  bomNumber?: string;
  fgItemCode?: string;
  fgItemName?: string;
  quantityPerUnit?: number;
  bomQuantity?: number;
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

export interface MaterialRequestItemLineDto {
  itemId?: string;
  itemName?: string | null;
  quantity?: number;
  uom?: string | null;
  warehouseId?: string | null;
  materialRequestItemId?: string;
}

export interface MaterialRequestItemsForSeDto {
  materialRequestId?: string;
  materialRequestNumber?: string | null;
  suggestedPurpose?: string;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  items?: MaterialRequestItemLineDto[];
}

export interface PendingTransferDto {
  pickListItemId?: string;
  itemId?: string;
  warehouseId?: string;
  pendingQty?: number;
  batchId?: string | null;
}

export interface PendingTransitTransferDto {
  stockEntryId?: string;
  entryNumber?: string;
  postingDate?: string;
  sourceWarehouseId?: string;
  sourceWarehouseName?: string | null;
  totalQuantity?: number;
  itemCount?: number;
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
  customerId?: string | null;
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

export interface PutawayRuleDto extends EntityDto<string> {
  companyId?: string;
  itemId?: string | null;
  itemGroupId?: string | null;
  warehouseId?: string;
  stockCapacity?: number;
  priority?: number;
  uom?: string | null;
  isEnabled?: boolean;
}

export interface QiTemplateDto extends EntityDto<string> {
  name?: string;
  description?: string | null;
  itemId?: string | null;
  bomId?: string | null;
  isEnabled?: boolean;
  parameterCount?: number;
  parameters?: QiTemplateParameterDto[];
}

export interface QiTemplateParameterDto extends EntityDto<string> {
  specification?: string;
  expectedValue?: string | null;
  minValue?: number | null;
  maxValue?: number | null;
  isNumeric?: boolean;
  formulaBased?: boolean;
  formula?: string | null;
  acceptanceCriteria?: string | null;
}

export interface RepostItemValuationDto extends EntityDto<string> {
  companyId?: string;
  basedOn?: number;
  itemId?: string | null;
  warehouseId?: string | null;
  postingDate?: string;
  status?: number;
  repostGlEntries?: boolean;
  totalAffectedEntries?: number;
  currentIndex?: number;
  errorLog?: string | null;
  voucherType?: string | null;
  voucherId?: string | null;
  isDeduplicated?: boolean;
  creationTime?: string;
}

export interface SerialAndBatchBundleDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  warehouseId?: string;
  typeOfTransaction?: string;
  bundleType?: string;
  voucherType?: string | null;
  voucherId?: string | null;
  postingDate?: string;
  totalQty?: number;
  totalAmount?: number;
  entryCount?: number;
  isCancelled?: boolean;
  entries?: BundleEntryDto[] | null;
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
  itemName?: string | null;
  warehouseName?: string | null;
  actualQty?: number;
  orderedQty?: number;
  plannedQty?: number;
  reservedQty?: number;
  indentedQty?: number;
  projectedQty?: number;
  stockValue?: number;
  valuationRate?: number;
}

export interface StockClosingBalanceDto {
  id?: string;
  itemId?: string;
  itemName?: string | null;
  warehouseId?: string;
  warehouseName?: string | null;
  qty?: number;
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
  balances?: StockClosingBalanceDto[] | null;
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
  itemName?: string | null;
  quantity?: number;
  sourceWarehouseId?: string | null;
  sourceWarehouseName?: string | null;
  targetWarehouseId?: string | null;
  targetWarehouseName?: string | null;
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

export interface StockMovementItemDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  openingQty?: number;
  stockInQty?: number;
  stockOutQty?: number;
  closingQty?: number;
  stockInValue?: number;
  stockOutValue?: number;
  netMovement?: number;
}

export interface StockMovementSummaryDto {
  fromDate?: string;
  toDate?: string;
  totalItems?: number;
  totalStockIn?: number;
  totalStockOut?: number;
  totalStockInValue?: number;
  totalStockOutValue?: number;
  items?: StockMovementItemDto[];
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

export interface TransitTransferItemDto {
  itemId?: string;
  quantity?: number;
  valuationRate?: number | null;
}

export interface UomDto {
  id?: string;
  uomName?: string;
  mustBeWholeNumber?: boolean;
  category?: string | null;
  isEnabled?: boolean;
}

export interface VariantAttributeDto {
  attributeId?: string;
  value?: string;
}

export interface VoucherStockLedgerDto {
  voucherType?: string;
  voucherId?: string;
  entries?: VoucherStockLedgerEntryDto[];
  totalQtyIn?: number;
  totalQtyOut?: number;
  totalValueDifference?: number;
}

export interface VoucherStockLedgerEntryDto {
  postingDate?: string;
  itemCode?: string | null;
  itemName?: string | null;
  warehouseName?: string;
  quantityChange?: number;
  valuationRate?: number;
  stockValueDifference?: number;
  balanceQuantity?: number;
  balanceValue?: number;
}

export interface WarehouseAccountDto extends EntityDto<string> {
  warehouseId?: string;
  warehouseName?: string | null;
  companyId?: string;
  accountId?: string;
  accountName?: string | null;
  stockReceivedButNotBilledAccountId?: string | null;
  stockDeliveredButNotBilledAccountId?: string | null;
  stockAdjustmentAccountId?: string | null;
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
