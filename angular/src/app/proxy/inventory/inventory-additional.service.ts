import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface BatchDto {
  id?: string;
  batchNo?: string;
  itemId?: string;
  expiryDate?: string;
  isDisabled?: boolean;
}

export interface SerialNoDto {
  id?: string;
  serialNumber?: string;
  itemId?: string;
  warehouseId?: string;
  maintenanceStatus?: string;
  status?: number;
}

@Injectable({ providedIn: 'root' })
export class BatchProxyService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BatchDto>>({
      method: 'GET', url: '/api/app/batch',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class SerialNoProxyService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SerialNoDto>>({
      method: 'GET', url: '/api/app/serial-no',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}
