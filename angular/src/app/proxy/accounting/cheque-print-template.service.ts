import type { ChequePrintPreviewDto, ChequePrintTemplateDto, CreateUpdateChequePrintTemplateDto, GetChequePrintTemplateListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ChequePrintTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateChequePrintTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChequePrintTemplateDto>({
      method: 'POST',
      url: '/api/app/cheque-print-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/cheque-print-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  generatePreview = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChequePrintPreviewDto>({
      method: 'POST',
      url: `/api/app/cheque-print-template/${id}/generate-preview`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChequePrintTemplateDto>({
      method: 'GET',
      url: `/api/app/cheque-print-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetChequePrintTemplateListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ChequePrintTemplateDto>>({
      method: 'GET',
      url: '/api/app/cheque-print-template',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateChequePrintTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChequePrintTemplateDto>({
      method: 'PUT',
      url: `/api/app/cheque-print-template/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}