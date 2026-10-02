import type { BarcodeScanResultDto, CreatePosInvoiceDto, ParentItemGroupDto, PosInvoiceDto, PosItemDto, PosItemSearchDto, ScanBarcodeInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PosService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  completeSale = (input: CreatePosInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosInvoiceDto>({
      method: 'POST',
      url: '/api/app/pos/complete-sale',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  scanBarcode = (input: ScanBarcodeInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BarcodeScanResultDto>({
      method: 'POST',
      url: '/api/app/pos/scan-barcode',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  searchItems = (input: PosItemSearchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PosItemDto>>({
      method: 'POST',
      url: '/api/app/pos/search-items',
      body: input,
    },
    { apiName: this.apiName,...config });

  getParentItemGroup = (posProfileId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ParentItemGroupDto>({
      method: 'GET',
      url: `/api/app/pos/parent-item-group/${posProfileId}`,
    },
    { apiName: this.apiName,...config });

  getItemGroups = (posProfileId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ParentItemGroupDto[]>({
      method: 'GET',
      url: `/api/app/pos/item-groups/${posProfileId}`,
    },
    { apiName: this.apiName,...config });
}