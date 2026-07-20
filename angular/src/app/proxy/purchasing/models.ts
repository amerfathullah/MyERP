import type { ScorecardPeriodType } from './scorecard-period-type.enum';
import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { SubcontractingOrderStatus } from './entities/subcontracting-order-status.enum';
import type { SubcontractingReceiptStatus } from './entities/subcontracting-receipt-status.enum';

export interface CreateCriterionDto {
  name?: string;
  weight?: number;
  maxScore?: number;
  formula?: string | null;
}

export interface CreatePurchaseInvoiceDto {
  companyId: string;
  supplierId: string;
  issueDate: string;
  dueDate?: string | null;
  paymentTermsTemplateId?: string | null;
  supplierInvoiceNumber?: string | null;
  currencyCode?: string;
  notes?: string | null;
  isOpening?: boolean;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  updateStock?: boolean;
  warehouseId?: string | null;
  items: CreatePurchaseInvoiceItemDto[];
}

export interface CreatePurchaseInvoiceItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreatePurchaseOrderDto {
  companyId: string;
  supplierId: string;
  orderDate: string;
  expectedDeliveryDate?: string | null;
  notes?: string | null;
  items: CreatePurchaseOrderItemDto[];
}

export interface CreatePurchaseOrderItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreatePurchaseReceiptDto {
  companyId: string;
  supplierId: string;
  warehouseId: string;
  postingDate: string;
  purchaseOrderId?: string | null;
  supplierDeliveryNote?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  notes?: string | null;
  items: CreatePurchaseReceiptItemDto[];
}

export interface CreatePurchaseReceiptItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
  purchaseOrderItemId?: string | null;
}

export interface CreateRfqDto {
  companyId?: string;
  transactionDate?: string;
  currencyCode?: string | null;
  messageForSupplier?: string | null;
  items?: CreateRfqItemDto[];
  suppliers?: CreateRfqSupplierDto[];
}

export interface CreateRfqItemDto {
  itemId?: string;
  description?: string;
  qty?: number;
  uom?: string;
}

export interface CreateRfqSupplierDto {
  supplierId?: string;
  email?: string | null;
}

export interface CreateSQItemDto {
  itemId?: string;
  itemName?: string | null;
  qty?: number;
  rate?: number;
}

export interface CreateScoItemDto {
  itemId: string;
  itemName: string;
  qty?: number;
  rate?: number;
  bomId?: string | null;
  warehouseId?: string | null;
}

export interface CreateScorecardDto {
  supplierId?: string;
  companyId?: string;
  periodType?: ScorecardPeriodType;
  weightingFunction?: string | null;
  standings?: CreateStandingDto[];
  criteria?: CreateCriterionDto[];
}

export interface CreateScorecardPeriodDto {
  startDate?: string;
  endDate?: string;
  score?: number;
}

export interface CreateScrItemDto {
  itemId: string;
  itemName: string;
  qty?: number;
  rate?: number;
  warehouseId?: string | null;
}

export interface CreateStandingDto {
  name?: string;
  minScore?: number;
  maxScore?: number;
  preventPos?: boolean;
  preventRfqs?: boolean;
  warnPos?: boolean;
  warnRfqs?: boolean;
}

export interface CreateSubcontractingOrderDto {
  companyId: string;
  supplierId: string;
  orderDate: string;
  purchaseOrderId?: string | null;
  notes?: string | null;
  items?: CreateScoItemDto[];
}

export interface CreateSubcontractingReceiptDto {
  companyId: string;
  supplierId: string;
  subcontractingOrderId: string;
  postingDate: string;
  warehouseId?: string | null;
  items?: CreateScrItemDto[];
}

export interface CreateSupplierQuotationDto {
  companyId?: string;
  supplierId?: string;
  supplierName?: string | null;
  transactionDate?: string;
  validTill?: string | null;
  currency?: string;
  requestForQuotationId?: string | null;
  items?: CreateSQItemDto[];
}

export interface CreateUpdateSupplierDto {
  companyId: string;
  name: string;
  supplierCode?: string | null;
  tin?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  idType?: string | null;
  idValue?: string | null;
  contactPerson?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  defaultPayableAccountId?: string | null;
  isActive?: boolean;
}

export interface GetScoListDto extends PagedAndSortedResultRequestDto {
  status?: SubcontractingOrderStatus | null;
  companyId?: string | null;
}

export interface GetSupplierListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface PurchaseInvoiceDto extends EntityDto<string> {
  companyId?: string;
  invoiceNumber?: string;
  supplierInvoiceNumber?: string | null;
  issueDate?: string;
  dueDate?: string | null;
  supplierId?: string;
  supplierTin?: string | null;
  currencyCode?: string;
  exchangeRate?: number;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountPaid?: number;
  outstandingAmount?: number;
  baseNetTotal?: number;
  baseTaxAmount?: number;
  baseGrandTotal?: number;
  baseOutstandingAmount?: number;
  status?: string;
  eInvoiceStatus?: string;
  lhdnUuid?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  amendedFromId?: string | null;
  amendmentIndex?: number;
  creditToAccountId?: string;
  supplierName?: string | null;
  items?: PurchaseInvoiceItemDto[];
}

export interface PurchaseInvoiceItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
}

export interface PurchaseOrderDto extends EntityDto<string> {
  companyId?: string;
  orderNumber?: string;
  orderDate?: string;
  expectedDeliveryDate?: string | null;
  supplierId?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  status?: string;
  perReceived?: number;
  perBilled?: number;
  items?: PurchaseOrderItemDto[];
}

export interface PurchaseOrderItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
  receivedQty?: number;
  billedQty?: number;
  warehouseId?: string | null;
}

export interface PurchaseReceiptDto extends EntityDto<string> {
  companyId?: string;
  receiptNumber?: string;
  postingDate?: string;
  supplierId?: string;
  purchaseOrderId?: string | null;
  warehouseId?: string;
  supplierDeliveryNote?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  status?: string;
  items?: PurchaseReceiptItemDto[];
}

export interface PurchaseReceiptItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
  purchaseOrderItemId?: string | null;
}

export interface PurchaseRegisterLineDto {
  invoiceId?: string;
  invoiceNumber?: string;
  postingDate?: string;
  supplierId?: string;
  supplierName?: string | null;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountPaid?: number;
  outstanding?: number;
  isReturn?: boolean;
}

export interface RfqDto {
  id?: string;
  companyId?: string;
  rfqNumber?: string;
  transactionDate?: string;
  currencyCode?: string;
  messageForSupplier?: string | null;
  status?: string;
  items?: RfqItemDto[];
  suppliers?: RfqSupplierDto[];
}

export interface RfqItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  qty?: number;
  uom?: string;
}

export interface RfqSupplierDto {
  id?: string;
  supplierId?: string;
  supplierName?: string;
  email?: string | null;
  emailSent?: boolean;
  quoteStatus?: string;
}

export interface ScoItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  qty?: number;
  rate?: number;
  receivedQty?: number;
}

export interface ScorecardCriterionDto {
  name?: string;
  weight?: number;
  maxScore?: number;
  formula?: string | null;
}

export interface ScorecardDto {
  id?: string;
  supplierId?: string;
  companyId?: string;
  periodType?: string;
  score?: number;
  currentStanding?: string | null;
  weightingFunction?: string | null;
  standings?: ScorecardStandingDto[];
  criteria?: ScorecardCriterionDto[];
}

export interface ScorecardStandingDto {
  name?: string;
  minScore?: number;
  maxScore?: number;
  preventPos?: boolean;
  preventRfqs?: boolean;
}

export interface SubcontractingOrderDto extends AuditedEntityDto<string> {
  orderNumber?: string;
  orderDate?: string;
  supplierId?: string;
  companyId?: string;
  netTotal?: number;
  grandTotal?: number;
  status?: SubcontractingOrderStatus;
  perReceived?: number;
  items?: ScoItemDto[];
}

export interface SubcontractingReceiptDto extends AuditedEntityDto<string> {
  receiptNumber?: string;
  postingDate?: string;
  supplierId?: string;
  subcontractingOrderId?: string;
  netTotal?: number;
  status?: SubcontractingReceiptStatus;
}

export interface SupplierDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  name?: string;
  supplierCode?: string | null;
  tin?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  idType?: string | null;
  idValue?: string | null;
  contactPerson?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  defaultPayableAccountId?: string | null;
  isActive?: boolean;
}

export interface SupplierQuotationDto extends EntityDto<string> {
  companyId?: string;
  supplierId?: string;
  supplierName?: string | null;
  quotationNumber?: string | null;
  transactionDate?: string;
  validTill?: string | null;
  currency?: string;
  netTotal?: number;
  grandTotal?: number;
  status?: number;
  items?: SupplierQuotationItemDto[];
}

export interface SupplierQuotationItemDto {
  id?: string;
  itemId?: string;
  itemName?: string | null;
  qty?: number;
  rate?: number;
  amount?: number;
}
