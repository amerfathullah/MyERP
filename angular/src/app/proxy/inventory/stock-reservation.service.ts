import type { CreateStockReservationDto, GetStockReservationListDto, StockReservationEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockReservationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReservationEntryDto>({
      method: 'POST',
      url: `/api/app/stock-reservation/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  cancelForOrder = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/stock-reservation/cancel-for-order/${salesOrderId}`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateStockReservationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReservationEntryDto>({
      method: 'POST',
      url: '/api/app/stock-reservation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockReservationEntryDto>({
      method: 'GET',
      url: `/api/app/stock-reservation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetStockReservationListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockReservationEntryDto>>({
      method: 'GET',
      url: '/api/app/stock-reservation',
      params: { itemId: input.itemId, warehouseId: input.warehouseId, voucherId: input.voucherId, status: input.status, companyId: input.companyId, filter: input.filter, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getReservedQty = (itemId: string, warehouseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/stock-reservation/reserved-qty',
      params: { itemId, warehouseId },
    },
    { apiName: this.apiName,...config });
}