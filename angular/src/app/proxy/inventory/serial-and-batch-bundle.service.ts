import type { GetBundleListDto, SerialAndBatchBundleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SerialAndBatchBundleService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SerialAndBatchBundleDto>({
      method: 'GET',
      url: `/api/app/serial-and-batch-bundle/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetBundleListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SerialAndBatchBundleDto>>({
      method: 'GET',
      url: '/api/app/serial-and-batch-bundle',
      params: { itemId: input.itemId, warehouseId: input.warehouseId, voucherType: input.voucherType, companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}