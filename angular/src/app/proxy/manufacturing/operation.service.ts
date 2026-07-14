import type { CreateOperationDto, OperationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OperationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateOperationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OperationDto>({
      method: 'POST',
      url: '/api/app/operation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OperationDto>>({
      method: 'GET',
      url: '/api/app/operation',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}