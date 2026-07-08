import type { FullAuditedEntityDto } from '@abp/ng.core';

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
