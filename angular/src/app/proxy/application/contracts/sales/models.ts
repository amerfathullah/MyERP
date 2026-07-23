import type { ProformaInvoiceBasis } from '../../../sales/proforma-invoice-basis.enum';
import type { ProformaInvoiceStatus } from '../../../sales/proforma-invoice-status.enum';

export interface CreateProformaInvoiceDto {
  salesOrderId: string;
  basedOn?: ProformaInvoiceBasis;
  hideItemQty?: boolean;
  items: CreateProformaInvoiceItemDto[];
}

export interface CreateProformaInvoiceItemDto {
  salesOrderItemId: string;
  quantity?: number;
  amount?: number | null;
}

export interface ProformaInvoiceDto {
  id?: string;
  proformaNumber?: string;
  proformaDate?: string;
  salesOrderId?: string;
  salesOrderNumber?: string | null;
  customerId?: string;
  customerName?: string | null;
  basedOn?: ProformaInvoiceBasis;
  hideItemQty?: boolean;
  currencyCode?: string | null;
  grandTotal?: number;
  totalQty?: number;
  status?: ProformaInvoiceStatus;
  proformaPdfUrl?: string | null;
  sentOn?: string | null;
  emailedTo?: string | null;
  items?: ProformaInvoiceItemDto[];
}

export interface ProformaInvoiceItemDto {
  id?: string;
  salesOrderItemId?: string;
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  uom?: string | null;
  quantity?: number;
  rate?: number;
  amount?: number;
}

export interface ProformedTotalsDto {
  salesOrderItemId?: string;
  itemCode?: string;
  itemName?: string;
  orderedQty?: number;
  orderedAmount?: number;
  proformedQty?: number;
  proformedAmount?: number;
  remainingQty?: number;
  remainingAmount?: number;
}

export interface SendProformaEmailDto {
  recipients: string;
}
