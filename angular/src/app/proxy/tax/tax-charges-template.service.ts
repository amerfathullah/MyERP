import type { CreateTaxChargesTemplateDto, GetTaxTemplateListDto, TaxChargesTemplateDto } from './models';
import type { TaxTemplateType } from './tax-template-type.enum';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TaxChargesTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateTaxChargesTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxChargesTemplateDto>({
      method: 'POST',
      url: '/api/app/tax-charges-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tax-charges-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxChargesTemplateDto>({
      method: 'GET',
      url: `/api/app/tax-charges-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getActiveTemplates = (companyId: string, templateType: TaxTemplateType, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxChargesTemplateDto[]>({
      method: 'GET',
      url: `/api/app/tax-charges-template/active-templates/${companyId}`,
      params: { templateType },
    },
    { apiName: this.apiName,...config });
  

  getDefault = (companyId: string, templateType: TaxTemplateType, taxCategoryId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxChargesTemplateDto>({
      method: 'GET',
      url: '/api/app/tax-charges-template/default',
      params: { companyId, templateType, taxCategoryId },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetTaxTemplateListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TaxChargesTemplateDto>>({
      method: 'GET',
      url: '/api/app/tax-charges-template',
      params: { companyId: input.companyId, templateType: input.templateType, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  toggleEnabled = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxChargesTemplateDto>({
      method: 'POST',
      url: `/api/app/tax-charges-template/${id}/toggle-enabled`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateTaxChargesTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxChargesTemplateDto>({
      method: 'PUT',
      url: `/api/app/tax-charges-template/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}