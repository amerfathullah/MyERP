import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateDeliveryNoteDto {
  companyId: string;
  customerId: string;
  warehouseId: string;
  postingDate: string;
  salesOrderId?: string | null;
  shippingAddress?: string | null;
  transporter?: string | null;
  trackingNumber?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  notes?: string | null;
  items: CreateDeliveryNoteItemDto[];
}

export interface CreateDeliveryNoteItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
  salesOrderItemId?: string | null;
}

export interface CreatePosInvoiceDto {
  companyId: string;
  customerId?: string | null;
  items: PosLineItemDto[];
  paymentMethod?: string;
  amountReceived?: number;
}

export interface CreateQuotationDto {
  companyId: string;
  customerId: string;
  issueDate: string;
  validUntil?: string | null;
  currencyCode?: string;
  terms?: string | null;
  notes?: string | null;
  items: CreateQuotationItemDto[];
}

export interface CreateQuotationItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreateSalesInvoiceDto {
  companyId: string;
  customerId: string;
  issueDate: string;
  dueDate?: string | null;
  currencyCode?: string;
  notes?: string | null;
  items: CreateSalesInvoiceItemDto[];
}

export interface CreateSalesInvoiceItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreateSalesOrderDto {
  companyId: string;
  customerId: string;
  orderDate: string;
  deliveryDate?: string | null;
  customerPoNumber?: string | null;
  currencyCode?: string;
  terms?: string | null;
  notes?: string | null;
  quotationId?: string | null;
  items: CreateSalesOrderItemDto[];
}

export interface CreateSalesOrderItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount?: number;
  uom?: string;
}

export interface CreateUpdateCustomerDto {
  companyId: string;
  name: string;
  customerCode?: string | null;
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
  defaultReceivableAccountId?: string | null;
  isActive?: boolean;
}

export interface CustomerDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  name?: string;
  customerCode?: string | null;
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
  defaultReceivableAccountId?: string | null;
  isActive?: boolean;
}

export interface DeliveryNoteDto extends EntityDto<string> {
  companyId?: string;
  deliveryNumber?: string;
  postingDate?: string;
  customerId?: string;
  salesOrderId?: string | null;
  warehouseId?: string;
  shippingAddress?: string | null;
  transporter?: string | null;
  trackingNumber?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  status?: string;
  items?: DeliveryNoteItemDto[];
}

export interface DeliveryNoteItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
  salesOrderItemId?: string | null;
}

export interface PosInvoiceDto extends EntityDto<string> {
  invoiceNumber?: string;
  issueDate?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountReceived?: number;
  change?: number;
  status?: string;
}

export interface PosItemDto {
  id?: string;
  itemCode?: string;
  itemName?: string;
  sellingPrice?: number;
  uom?: string;
  barcode?: string | null;
}

export interface PosItemSearchDto {
  search?: string | null;
  maxResultCount?: number;
}

export interface PosLineItemDto {
  itemId: string;
  description?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
}

export interface QuotationDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  quotationNumber?: string;
  issueDate?: string;
  validUntil?: string | null;
  customerId?: string;
  customerName?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  terms?: string | null;
  notes?: string | null;
  status?: string;
  convertedToSalesOrderId?: string | null;
  items?: QuotationItemDto[];
}

export interface QuotationItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
}

export interface SalesInvoiceDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  invoiceNumber?: string;
  issueDate?: string;
  dueDate?: string | null;
  customerId?: string;
  customerName?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountPaid?: number;
  outstandingAmount?: number;
  status?: string;
  eInvoiceStatus?: string | null;
  lhdnUuid?: string | null;
  items?: SalesInvoiceItemDto[];
}

export interface SalesInvoiceItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
}

export interface SalesOrderDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  orderNumber?: string;
  orderDate?: string;
  deliveryDate?: string | null;
  customerId?: string;
  customerName?: string | null;
  customerPoNumber?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  terms?: string | null;
  notes?: string | null;
  status?: string;
  quotationId?: string | null;
  items?: SalesOrderItemDto[];
}

export interface SalesOrderItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  uom?: string;
  quantity?: number;
  unitPrice?: number;
  taxAmount?: number;
  lineTotal?: number;
}
