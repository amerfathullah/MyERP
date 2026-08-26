import type { CommonCodeDto, CreateUpdateCommonCodeDto, GetCommonCodeListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CommonCodeService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateCommonCodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CommonCodeDto>({
      method: 'POST',
      url: '/api/app/common-code',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/common-code/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CommonCodeDto>({
      method: 'GET',
      url: `/api/app/common-code/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetCommonCodeListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CommonCodeDto>>({
      method: 'GET',
      url: '/api/app/common-code',
      params: { codeListId: input.codeListId, filter: input.filter, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateCommonCodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CommonCodeDto>({
      method: 'PUT',
      url: `/api/app/common-code/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
