import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';

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
