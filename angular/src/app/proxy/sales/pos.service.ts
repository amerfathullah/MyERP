import type { CreatePosInvoiceDto, PosInvoiceDto, PosItemDto, PosItemSearchDto } from './models';
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
  

  searchItems = (input: PosItemSearchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PosItemDto>>({
      method: 'POST',
      url: '/api/app/pos/search-items',
      body: input,
    },
    { apiName: this.apiName,...config });
}