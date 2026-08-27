import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { BarcodeType } from './barcode-type.enum';
import type { MaterialRequestType } from '../purchasing/material-request-type.enum';
import type { QualityFeedbackDocumentType } from './quality-feedback-document-type.enum';
import type { QualityReviewStatus } from './quality-review-status.enum';
import type { StockEntryType } from './stock-entry-type.enum';
import type { ItemType } from './item-type.enum';
import type { ValuationMethod } from './valuation-method.enum';
import type { QualityActionType } from './quality-action-type.enum';
import type { DeliveryTripStatus } from './delivery-trip-status.enum';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';
import type { NonConformanceStatus } from './non-conformance-status.enum';
import type { QualityActionStatus } from './quality-action-status.enum';
import type { QualityMeetingStatus } from './quality-meeting-status.enum';
import type { WarehouseType } from './warehouse-type.enum';

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

export interface BatchCustomerSummaryDto {
  customerId?: string;
  customerName?: string;
  totalQuantity?: number;
  deliveryCount?: number;
  firstDeliveryDate?: string;
  lastDeliveryDate?: string;
}

export interface BatchDeliveryTraceDto {
  deliveryNoteId?: string;
  deliveryNumber?: string | null;
  deliveryDate?: string;
  customerId?: string;
  customerName?: string;
  quantityDelivered?: number;
  warehouseId?: string;
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

export interface BatchMovementHistoryDto {
  batchId?: string;
  batchNo?: string;
  entries?: BatchMovementEntryDto[];
}

export interface BatchStockBalanceDto {
  batchId?: string;
  batchNo?: string;
  itemId?: string;
  totalQuantity?: number;
  totalValue?: number;
  warehouseBalances?: BatchWarehouseBalanceDto[];
}

export interface BatchTraceabilityDto {
  batchId?: string;
  batchNo?: string;
  itemId?: string;
  manufacturingDate?: string | null;
  expiryDate?: string | null;
  totalProduced?: number;
  totalDelivered?: number;
  customerCount?: number;
  deliveries?: BatchDeliveryTraceDto[];
  customerSummary?: BatchCustomerSummaryDto[];
}

export interface BatchWarehouseBalanceDto {
  warehouseId?: string;
  warehouseName?: string;
  quantity?: number;
  stockValue?: number;
  valuationRate?: number;
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

export interface BrandDto extends EntityDto<string> {
  name?: string;
  description?: string | null;
  defaultWarehouseId?: string | null;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  isActive?: boolean;
}

export interface BulkPriceUpdateDto {
  priceListId?: string;
  percentageChange?: number;
  itemGroupId?: string | null;
}

export interface BulkPriceUpdateResultDto {
  updatedCount?: number;
  percentageApplied?: number;
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

export interface CreateItemBarcodeDto {
  barcode: string;
  barcodeType?: BarcodeType;
  isDefault?: boolean;
}

export interface CreateItemCustomerDetailDto {
  customerId: string;
  refCode: string;
}

export interface CreateItemGroupDto {
  name?: string;
  parentId?: string | null;
  isGroup?: boolean;
  defaultWarehouseId?: string | null;
}

export interface CreateItemReorderDto {
  warehouseId: string;
  warehouseGroupId?: string | null;
  warehouseReorderLevel?: number;
  warehouseReorderQty?: number;
  materialRequestType?: MaterialRequestType;
}

export interface CreateItemStandardCostDto {
  companyId?: string;
  itemId?: string;
  standardRate?: number;
  effectiveDate?: string;
}

export interface CreateItemSupplierDto {
  supplierId: string;
  supplierPartNo?: string | null;
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

export interface CreateQualityActionResolutionDto {
  problem?: string;
  resolutionDetails?: string;
}

export interface CreateQualityFeedbackDto {
  companyId?: string;
  documentType?: QualityFeedbackDocumentType;
  documentName?: string;
  templateId?: string;
  remarks?: string | null;
  parameters?: CreateQualityFeedbackParameterDto[];
}

export interface CreateQualityFeedbackParameterDto {
  parameter?: string;
  rating?: number;
  remarks?: string | null;
}

export interface CreateQualityMeetingMinutesDto {
  discussion?: string;
  actionPlan?: string | null;
  assignedUserId?: string | null;
}

export interface CreateQualityReviewDto {
  qualityGoalId?: string;
  procedureId?: string | null;
  reviewDate?: string;
  actualValue?: number | null;
  notes?: string | null;
  reviewedByUserId?: string | null;
  objectives?: CreateQualityReviewObjectiveDto[];
}

export interface CreateQualityReviewObjectiveDto {
  objective?: string;
  target?: number;
  actual?: number | null;
  uom?: string | null;
  status?: QualityReviewStatus;
  notes?: string | null;
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

export interface CreateUpdateBrandDto {
  name?: string;
  description?: string | null;
  defaultWarehouseId?: string | null;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  isActive?: boolean;
}

export interface CreateUpdateCustomsTariffNumberDto {
  companyId: string;
  tariffNumber: string;
  description?: string | null;
}

export interface CreateUpdateDeliveryStopDto {
  id?: string | null;
  customerId?: string | null;
  customerName?: string | null;
  address: string;
  customerAddress?: string | null;
  locked?: boolean;
  visited?: boolean;
  deliveryNoteId?: string | null;
  deliveryNoteNumber?: string | null;
  grandTotal?: number;
  contactName?: string | null;
  emailSentTo?: string | null;
  customerContact?: string | null;
  distance?: number;
  uom?: string | null;
  estimatedArrival?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  details?: string | null;
}

export interface CreateUpdateDeliveryTripDto {
  companyId: string;
  namingSeries?: string | null;
  tripNumber: string;
  driver: string;
  driverName?: string | null;
  driverEmail?: string | null;
  driverAddress?: string | null;
  vehicle: string;
  departureTime: string;
  employeeId?: string | null;
  uom?: string | null;
  deliveryStops?: CreateUpdateDeliveryStopDto[];
}

export interface CreateUpdateItemAlternativeDto {
  companyId: string;
  itemId: string;
  alternativeItemId: string;
  twoWay?: boolean;
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
  grantCommission?: boolean;
  maxDiscount?: number | null;
  isActive?: boolean;
  reorderLevel?: number;
  reorderQty?: number;
  safetyStock?: number;
  defaultWarehouseId?: string | null;
  minOrderQty?: number;
  inspectionRequiredBeforePurchase?: boolean;
  inspectionRequiredBeforeDelivery?: boolean;
  customsTariffNumberId?: string | null;
  allowAlternativeItem?: boolean;
  defaultManufacturerId?: string | null;
  defaultManufacturerPartNo?: string | null;
  barcodes?: CreateItemBarcodeDto[];
  suppliers?: CreateItemSupplierDto[];
  customerDetails?: CreateItemCustomerDetailDto[];
  reorders?: CreateItemReorderDto[];
}

export interface CreateUpdateItemManufacturerDto {
  companyId: string;
  itemId: string;
  manufacturerId: string;
  manufacturerPartNo: string;
  description?: string | null;
  isDefault?: boolean;
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

export interface CreateUpdateManufacturerDto {
  companyId: string;
  shortName: string;
  fullName?: string | null;
  website?: string | null;
  country?: string | null;
  logoUrl?: string | null;
  notes?: string | null;
}

export interface CreateUpdateNonConformanceDto {
  companyId?: string;
  subject?: string;
  procedureId?: string | null;
  processOwner?: string | null;
  details?: string | null;
  correctiveAction?: string | null;
  preventiveAction?: string | null;
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

export interface CreateUpdateQualityActionDto {
  companyId?: string;
  actionType?: QualityActionType;
  problemDescription?: string;
  relatedQualityGoalId?: string | null;
  relatedQualityReviewId?: string | null;
  relatedProcedureId?: string | null;
  relatedFeedbackId?: string | null;
  assignedUserId?: string | null;
  resolutions?: CreateQualityActionResolutionDto[];
}

export interface CreateUpdateQualityFeedbackTemplateDto {
  templateName?: string;
  parameters?: string[];
}

export interface CreateUpdateQualityGoalDto {
  name?: string;
  goal?: string | null;
  frequency?: string;
  targetValue?: number;
  uom?: string | null;
  responsibleUserId?: string | null;
  procedureId?: string | null;
  weekday?: string | null;
  dayOfMonth?: number | null;
  isEnabled?: boolean;
  objectives?: CreateUpdateQualityGoalObjectiveDto[];
}

export interface CreateUpdateQualityGoalObjectiveDto {
  objective?: string;
  target?: number;
  uom?: string | null;
}

export interface CreateUpdateQualityMeetingDto {
  companyId?: string;
  meetingDate?: string;
  chairperson?: string | null;
  attendees?: string | null;
  agendas?: string[];
  minutes?: CreateQualityMeetingMinutesDto[];
}

export interface CreateUpdateQualityProcedureDto {
  name?: string;
  parentQualityProcedureId?: string | null;
  isGroup?: boolean;
  description?: string | null;
  processOwner?: string | null;
  sequence?: number;
  steps?: CreateUpdateQualityProcedureStepDto[];
}

export interface CreateUpdateQualityProcedureStepDto {
  description?: string;
  sequence?: number;
  childProcedureId?: string | null;
}

export interface CreateUpdateUomCategoryDto {
  name: string;
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
  warehouseType?: WarehouseType;
}

export interface CreateWarehouseAccountDto {
  warehouseId: string;
  companyId: string;
  accountId: string;
  stockReceivedButNotBilledAccountId?: string | null;
  stockDeliveredButNotBilledAccountId?: string | null;
  stockAdjustmentAccountId?: string | null;
}

export interface CustomsTariffNumberDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  tariffNumber?: string;
  description?: string | null;
}

export interface DeliveryStopDto extends FullAuditedEntityDto<string> {
  deliveryTripId?: string;
  customerId?: string | null;
  customerName?: string | null;
  address?: string;
  customerAddress?: string | null;
  locked?: boolean;
  visited?: boolean;
  deliveryNoteId?: string | null;
  deliveryNoteNumber?: string | null;
  grandTotal?: number;
  contactName?: string | null;
  emailSentTo?: string | null;
  customerContact?: string | null;
  distance?: number;
  uom?: string | null;
  estimatedArrival?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  details?: string | null;
}

export interface DeliveryTripDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  namingSeries?: string | null;
  tripNumber?: string;
  driver?: string;
  driverName?: string | null;
  driverEmail?: string | null;
  driverAddress?: string | null;
  vehicle?: string;
  departureTime?: string;
  employeeId?: string | null;
  totalDistance?: number;
  uom?: string | null;
  emailNotificationSent?: boolean;
  status?: DeliveryTripStatus;
  deliveryStops?: DeliveryStopDto[];
}

export interface EvaluateQualityReviewDto {
  actualValue?: number | null;
  notes?: string | null;
  passed?: boolean;
}

export interface EvaluateGoalDto {
  actualValue: number;
  reviewDate: string;
  notes?: string | null;
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
  customerId?: string | null;
  supplierId?: string | null;
}

export interface GetItemPriceListDto extends PagedAndSortedResultRequestDto {
  itemId?: string | null;
  priceListId?: string | null;
  customerId?: string | null;
  supplierId?: string | null;
  filter?: string | null;
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

export interface ItemAlternativeDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  itemId?: string;
  itemCode?: string | null;
  itemName?: string | null;
  alternativeItemId?: string;
  alternativeItemCode?: string | null;
  alternativeItemName?: string | null;
  twoWay?: boolean;
}

export interface ItemAttributeDto extends EntityDto<string> {
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

export interface ItemBarcodeDto extends EntityDto<string> {
  barcode?: string;
  barcodeType?: BarcodeType;
  isDefault?: boolean;
}

export interface ItemCustomerDetailDto extends EntityDto<string> {
  customerId?: string;
  refCode?: string;
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
  grantCommission?: boolean;
  maxDiscount?: number | null;
  isActive?: boolean;
  reorderLevel?: number;
  reorderQty?: number;
  safetyStock?: number;
  defaultWarehouseId?: string | null;
  minOrderQty?: number;
  inspectionRequiredBeforePurchase?: boolean;
  inspectionRequiredBeforeDelivery?: boolean;
  customsTariffNumberId?: string | null;
  allowAlternativeItem?: boolean;
  defaultManufacturerId?: string | null;
  defaultManufacturerPartNo?: string | null;
  totalStockQty?: number;
  isLowStock?: boolean;
  hasSerialNo?: boolean;
  hasBatchNo?: boolean;
  hasVariants?: boolean;
  leadTimeDays?: number;
  barcodes?: ItemBarcodeDto[];
  suppliers?: ItemSupplierDto[];
  customerDetails?: ItemCustomerDetailDto[];
  reorders?: ItemReorderDto[];
}

export interface ItemGroupDto extends EntityDto<string> {
  name?: string;
  parentId?: string | null;
  isGroup?: boolean;
  defaultWarehouseId?: string | null;
}

export interface ItemManufacturerDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  itemId?: string;
  itemCode?: string | null;
  itemName?: string | null;
  manufacturerId?: string;
  manufacturerShortName?: string | null;
  manufacturerPartNo?: string;
  description?: string | null;
  isDefault?: boolean;
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
  itemCode?: string | null;
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
  customerName?: string | null;
  supplierId?: string | null;
  supplierName?: string | null;
  batchNo?: string | null;
  isAutoInserted?: boolean;
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

export interface ItemReorderDto extends EntityDto<string> {
  warehouseId?: string;
  warehouseGroupId?: string | null;
  warehouseReorderLevel?: number;
  warehouseReorderQty?: number;
  materialRequestType?: MaterialRequestType;
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

export interface ItemSupplierDto extends EntityDto<string> {
  supplierId?: string;
  supplierPartNo?: string | null;
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

export interface ManufacturerDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  shortName?: string;
  fullName?: string | null;
  website?: string | null;
  country?: string | null;
  logoUrl?: string | null;
  notes?: string | null;
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

export interface NonConformanceDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  subject?: string;
  procedureId?: string | null;
  processOwner?: string | null;
  details?: string | null;
  correctiveAction?: string | null;
  preventiveAction?: string | null;
  status?: NonConformanceStatus;
  resolutionDate?: string | null;
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
  priceListName?: string;
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

export interface QualityActionDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  actionType?: QualityActionType;
  problemDescription?: string;
  resolution?: string | null;
  status?: QualityActionStatus;
  relatedQualityGoalId?: string | null;
  relatedQualityReviewId?: string | null;
  relatedProcedureId?: string | null;
  relatedFeedbackId?: string | null;
  assignedUserId?: string | null;
  resolutions?: QualityActionResolutionDto[];
}

export interface QualityActionResolutionDto extends EntityDto<string> {
  qualityActionId?: string;
  problem?: string;
  resolutionDetails?: string;
  status?: QualityActionStatus;
}

export interface QualityFeedbackDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  documentType?: QualityFeedbackDocumentType;
  documentName?: string;
  templateId?: string;
  remarks?: string | null;
  parameters?: QualityFeedbackParameterDto[];
}

export interface QualityFeedbackParameterDto extends EntityDto<string> {
  qualityFeedbackId?: string;
  parameter?: string;
  rating?: number;
  remarks?: string | null;
}

export interface QualityFeedbackTemplateDto extends FullAuditedEntityDto<string> {
  templateName?: string;
  parameters?: QualityFeedbackTemplateParameterDto[];
}

export interface QualityFeedbackTemplateParameterDto extends EntityDto<string> {
  qualityFeedbackTemplateId?: string;
  parameter?: string;
}

export interface QualityGoalDto extends FullAuditedEntityDto<string> {
  name?: string;
  goal?: string | null;
  frequency?: string;
  targetValue?: number;
  uom?: string | null;
  responsibleUserId?: string | null;
  procedureId?: string | null;
  weekday?: string | null;
  dayOfMonth?: number | null;
  isEnabled?: boolean;
  objectives?: QualityGoalObjectiveDto[];
}

export interface QualityGoalObjectiveDto extends EntityDto<string> {
  qualityGoalId?: string;
  objective?: string;
  target?: number;
  uom?: string | null;
}

export interface QualityMeetingAgendaDto extends EntityDto<string> {
  qualityMeetingId?: string;
  agenda?: string;
}

export interface QualityMeetingDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  meetingDate?: string;
  chairperson?: string | null;
  attendees?: string | null;
  status?: QualityMeetingStatus;
  agendas?: QualityMeetingAgendaDto[];
  minutes?: QualityMeetingMinutesDto[];
}

export interface QualityMeetingMinutesDto extends EntityDto<string> {
  qualityMeetingId?: string;
  discussion?: string;
  actionPlan?: string | null;
  assignedUserId?: string | null;
}

export interface QualityProcedureDto extends FullAuditedEntityDto<string> {
  name?: string;
  parentQualityProcedureId?: string | null;
  isGroup?: boolean;
  description?: string | null;
  processOwner?: string | null;
  sequence?: number;
  steps?: QualityProcedureStepDto[];
}

export interface QualityProcedureStepDto extends EntityDto<string> {
  qualityProcedureId?: string;
  description?: string;
  sequence?: number;
  childProcedureId?: string | null;
}

export interface QualityReviewDto extends FullAuditedEntityDto<string> {
  qualityGoalId?: string;
  procedureId?: string | null;
  reviewDate?: string;
  actualValue?: number | null;
  status?: QualityReviewStatus;
  notes?: string | null;
  reviewedByUserId?: string | null;
  objectives?: QualityReviewObjectiveDto[];
}

export interface QualityReviewObjectiveDto extends EntityDto<string> {
  qualityReviewId?: string;
  objective?: string;
  target?: number;
  actual?: number | null;
  uom?: string | null;
  status?: QualityReviewStatus;
  notes?: string | null;
}

export interface ReorderSuggestionDto {
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  currentReorderLevel?: number;
  suggestedReorderLevel?: number;
  suggestedReorderQty?: number;
  suggestedSafetyStock?: number;
  avgDailyConsumption?: number;
  currentStock?: number;
  daysOfStockRemaining?: number;
  leadTimeDays?: number;
  isUnderstocked?: boolean;
  isOverstocked?: boolean;
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

export interface ResolveNonConformanceDto {
  correctiveAction?: string | null;
  preventiveAction?: string | null;
}

export interface ResolveQualityActionDto {
  resolution?: string;
}

export interface SerialAndBatchBundleDto extends EntityDto<string> {
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

export interface StockBalanceDto extends EntityDto<string> {
  itemId?: string;
  warehouseId?: string;
  itemName?: string | null;
  warehouseName?: string | null;
  actualQty?: number;
  orderedQty?: number;
  plannedQty?: number;
  reservedQty?: number;
  reservedQtyForProduction?: number;
  reservedQtyForSubContract?: number;
  reservedQtyForProductionPlan?: number;
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

export interface StockGlComparisonDto {
  totalStockValue?: number;
  totalGlBalance?: number;
  difference?: number;
  isMatched?: boolean;
  warehouseCount?: number;
  itemCount?: number;
  asOfDate?: string;
  perWarehouse?: StockGlWarehouseComparisonDto[];
}

export interface StockGlComparisonRequestDto {
  companyId?: string;
  asOfDate?: string | null;
}

export interface StockGlWarehouseComparisonDto {
  warehouseId?: string;
  warehouseName?: string;
  stockValue?: number;
  glBalance?: number;
  difference?: number;
  hasMismatch?: boolean;
  stockAccountId?: string | null;
  stockAccountName?: string | null;
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

export interface UomCategoryDto extends FullAuditedEntityDto<string> {
  name?: string;
}

export interface UomDto extends EntityDto<string> {
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
  warehouseType?: WarehouseType;
}

export interface UomConversionDto extends EntityDto<string> {
  fromUom?: string;
  toUom?: string;
  conversionFactor?: number;
  itemId?: string | null;
}

export interface QualityInspectionParameterGroupDto extends FullAuditedEntityDto<string> {
  groupName: string;
  description?: string | null;
  isActive: boolean;
}

export interface CreateUpdateQualityInspectionParameterGroupDto {
  groupName: string;
  description?: string | null;
  isActive?: boolean;
}

export interface GetQualityInspectionParameterGroupListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface ShipmentParcelTemplateDto extends FullAuditedEntityDto<string> {
  parcelTemplateName: string;
  length: number;
  width: number;
  height: number;
  weight: number;
  description?: string | null;
  isActive: boolean;
}

export interface CreateUpdateShipmentParcelTemplateDto {
  parcelTemplateName: string;
  length?: number;
  width?: number;
  height?: number;
  weight?: number;
  description?: string | null;
  isActive?: boolean;
}

export interface GetShipmentParcelTemplateListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface ItemLeadTimeDto extends FullAuditedEntityDto<string> {
  itemId: string;
  itemCode?: string | null;
  itemName?: string | null;
  stockUom?: string | null;
  shiftTimeInHours: number;
  noOfWorkstations: number;
  noOfShifts: number;
  totalWorkstationTime: number;
  manufacturingTimeInMins: number;
  dailyYield: number;
  noOfUnitsProduced: number;
  capacityPerDay: number;
  purchaseTimeDays: number;
  bufferTimeDays: number;
  suppliers: ItemLeadTimeSupplierDto[];
}

export interface ItemLeadTimeSupplierDto extends FullAuditedEntityDto<string> {
  itemLeadTimeId: string;
  supplierId: string;
  supplierName?: string | null;
  purchaseTimeDays: number;
  bufferTimeDays: number;
  isDefault: boolean;
}

export interface CreateUpdateItemLeadTimeDto {
  itemId: string;
  shiftTimeInHours?: number;
  noOfWorkstations?: number;
  noOfShifts?: number;
  manufacturingTimeInMins?: number;
  dailyYield?: number;
  purchaseTimeDays?: number;
  bufferTimeDays?: number;
  suppliers?: CreateUpdateItemLeadTimeSupplierDto[];
}

export interface CreateUpdateItemLeadTimeSupplierDto {
  supplierId: string;
  purchaseTimeDays?: number;
  bufferTimeDays?: number;
  isDefault?: boolean;
}

export interface GetItemLeadTimeListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  itemId?: string | null;
}
