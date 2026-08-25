import type { CreateUpdateTermsAndConditionsDto, GetTermsAndConditionsListDto, TermsAndConditionsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TermsAndConditionsService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateTermsAndConditionsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TermsAndConditionsDto>({
      method: 'POST',
      url: '/api/app/terms-and-conditions',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/terms-and-conditions/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TermsAndConditionsDto>({
      method: 'GET',
      url: `/api/app/terms-and-conditions/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetTermsAndConditionsListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TermsAndConditionsDto>>({
      method: 'GET',
      url: '/api/app/terms-and-conditions',
      params: { companyId: input.companyId, isSelling: input.isSelling, isBuying: input.isBuying, isDisabled: input.isDisabled, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateTermsAndConditionsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TermsAndConditionsDto>({
      method: 'PUT',
      url: `/api/app/terms-and-conditions/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
