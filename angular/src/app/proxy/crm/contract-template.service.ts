import type { ContractTemplateDto, CreateUpdateContractTemplateDto, GetContractTemplateListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ContractTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateContractTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractTemplateDto>({
      method: 'POST',
      url: '/api/app/contract-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/contract-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractTemplateDto>({
      method: 'GET',
      url: `/api/app/contract-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetContractTemplateListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ContractTemplateDto>>({
      method: 'GET',
      url: '/api/app/contract-template',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateContractTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContractTemplateDto>({
      method: 'PUT',
      url: `/api/app/contract-template/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}