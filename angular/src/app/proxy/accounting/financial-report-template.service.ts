import type { CreateFinancialReportTemplateDto, ExecuteReportDto, FinancialReportResultDto, FinancialReportTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class FinancialReportTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateFinancialReportTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinancialReportTemplateDto>({
      method: 'POST',
      url: '/api/app/financial-report-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/financial-report-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  execute = (input: ExecuteReportDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinancialReportResultDto>({
      method: 'POST',
      url: '/api/app/financial-report-template/execute',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FinancialReportTemplateDto>({
      method: 'GET',
      url: `/api/app/financial-report-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<FinancialReportTemplateDto>>({
      method: 'GET',
      url: '/api/app/financial-report-template',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  toggle = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/financial-report-template/${id}/toggle`,
    },
    { apiName: this.apiName,...config });
  

  validate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'POST',
      url: `/api/app/financial-report-template/${id}/validate`,
    },
    { apiName: this.apiName,...config });
}