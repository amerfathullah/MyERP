import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface CreatePosInvoiceDto {
  companyId: string;
  customerId?: string;
  items: PosLineItemDto[];
  paymentMethod?: string;
  amountReceived: number;
}

export interface PosLineItemDto {
  itemId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxAmount: number;
}

export interface PosInvoiceDto {
  id?: string;
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
  barcode?: string;
}

export interface PosItemSearchDto {
  search?: string;
  maxResultCount?: number;
}

@Injectable({ providedIn: 'root' })
export class PosService {
  private restService = inject(RestService);
  apiName = 'Default';

  completeSale = (input: CreatePosInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosInvoiceDto>({
      method: 'POST',
      url: '/api/app/pos/complete-sale',
      body: input,
    }, { apiName: this.apiName, ...config });

  searchItems = (input: PosItemSearchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PosItemDto>>({
      method: 'GET',
      url: '/api/app/pos/search-items',
      params: { search: input.search, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}
