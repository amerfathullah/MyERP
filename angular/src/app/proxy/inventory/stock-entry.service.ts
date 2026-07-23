import type { CreateStockEntryDto, CreateTransitTransferDto, ManufactureItemsDto, PendingTransitTransferDto, StockEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class StockEntryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: `/api/app/stock-entry/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateStockEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: '/api/app/stock-entry',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createMaterialTransferForManufacture = (workOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: `/api/app/stock-entry/material-transfer-for-manufacture/${workOrderId}`,
    },
    { apiName: this.apiName,...config });
  

  createReceiveAtWarehouse = (outgoingStockEntryId: string, destinationWarehouseId: string, postingDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: '/api/app/stock-entry/receive-at-warehouse',
      params: { outgoingStockEntryId, destinationWarehouseId, postingDate },
    },
    { apiName: this.apiName,...config });
  

  createTransitTransfer = (input: CreateTransitTransferDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: '/api/app/stock-entry/transit-transfer',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/stock-entry/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'GET',
      url: `/api/app/stock-entry/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StockEntryDto>>({
      method: 'GET',
      url: '/api/app/stock-entry',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getManufactureItems = (workOrderId: string, produceQty: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ManufactureItemsDto>({
      method: 'GET',
      url: `/api/app/stock-entry/manufacture-items/${workOrderId}`,
      params: { produceQty },
    },
    { apiName: this.apiName,...config });
  

  getPendingTransitTransfers = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PendingTransitTransferDto[]>({
      method: 'GET',
      url: `/api/app/stock-entry/pending-transit-transfers/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  post = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: `/api/app/stock-entry/${id}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'POST',
      url: `/api/app/stock-entry/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateStockEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockEntryDto>({
      method: 'PUT',
      url: `/api/app/stock-entry/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}