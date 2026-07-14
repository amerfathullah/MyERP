import type { GetSerialNoListDto, SerialNoDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SerialNoService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SerialNoDto>({
      method: 'GET',
      url: `/api/app/serial-no/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetSerialNoListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SerialNoDto>>({
      method: 'GET',
      url: '/api/app/serial-no',
      params: { itemId: input.itemId, warehouseId: input.warehouseId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}