import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface CreatePurchaseInvoiceDto {
  companyId: string;
  supplierId: string;
  issueDate: string;
  dueDate?: string | null;
  supplierInvoiceNumber?: string | null;
  currencyCode?: string;
  notes?: string | null;
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

export interface PurchaseInvoiceDto extends EntityDto<string> {
  companyId?: string;
  invoiceNumber?: string;
  supplierInvoiceNumber?: string | null;
  issueDate?: string;
  dueDate?: string | null;
  supplierId?: string;
  supplierTin?: string | null;
  currencyCode?: string;
  netTotal?: number;
  taxAmount?: number;
  grandTotal?: number;
  amountPaid?: number;
  outstandingAmount?: number;
  status?: string;
  eInvoiceStatus?: string;
  lhdnUuid?: string | null;
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

// Material Request
export interface MaterialRequestDto {
  id?: string;
  requestNumber?: string;
  requestType?: number;
  status?: number;
  requestDate?: string;
  requiredByDate?: string | null;
  companyId?: string;
  workOrderId?: string | null;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  notes?: string | null;
  creationTime?: string;
  items?: MaterialRequestItemDto[];
}

export interface MaterialRequestItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  quantity?: number;
  orderedQuantity?: number;
  receivedQuantity?: number;
  uom?: string;
  warehouseId?: string | null;
}

export interface CreateMaterialRequestDto {
  companyId: string;
  requestType: number;
  requestDate: string;
  requiredByDate?: string | null;
  workOrderId?: string | null;
  sourceWarehouseId?: string | null;
  targetWarehouseId?: string | null;
  notes?: string | null;
  items: CreateMaterialRequestItemDto[];
}

export interface CreateMaterialRequestItemDto {
  itemId: string;
  itemName: string;
  quantity: number;
  uom?: string;
  warehouseId?: string | null;
}

export interface GetMaterialRequestListDto {
  requestType?: number | null;
  companyId?: string | null;
  filter?: string | null;
  skipCount?: number;
  maxResultCount?: number;
  sorting?: string;
}
