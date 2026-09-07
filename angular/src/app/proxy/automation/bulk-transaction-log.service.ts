import type { BulkTransactionLogDto, CreateBulkTransactionLogDto, GetBulkTransactionLogListDto, RecordBulkTransactionResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BulkTransactionLogService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateBulkTransactionLogDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkTransactionLogDto>({
      method: 'POST',
      url: '/api/app/bulk-transaction-log',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/bulk-transaction-log/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkTransactionLogDto>({
      method: 'GET',
      url: `/api/app/bulk-transaction-log/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetBulkTransactionLogListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BulkTransactionLogDto>>({
      method: 'GET',
      url: '/api/app/bulk-transaction-log',
      params: { filter: input.filter, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  recordDetailResult = (id: string, detailId: string, input: RecordBulkTransactionResultDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkTransactionLogDto>({
      method: 'POST',
      url: `/api/app/bulk-transaction-log/${id}/record-detail-result/${detailId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  retryDetail = (id: string, detailId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkTransactionLogDto>({
      method: 'POST',
      url: `/api/app/bulk-transaction-log/${id}/retry-detail/${detailId}`,
    },
    { apiName: this.apiName,...config });
}