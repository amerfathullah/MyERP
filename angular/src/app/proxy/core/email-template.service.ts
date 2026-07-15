import type { CreateEmailTemplateDto, EmailTemplateDto, RenderedTemplateDto, UpdateEmailTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EmailTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateEmailTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailTemplateDto>({
      method: 'POST',
      url: '/api/app/email-template',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/email-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailTemplateDto>({
      method: 'GET',
      url: `/api/app/email-template/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (documentType?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailTemplateDto[]>({
      method: 'GET',
      url: '/api/app/email-template',
      params: { documentType },
    },
    { apiName: this.apiName,...config });
  

  preview = (id: string, variables: Record<string, string>, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RenderedTemplateDto>({
      method: 'POST',
      url: `/api/app/email-template/${id}/preview`,
      body: variables,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateEmailTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailTemplateDto>({
      method: 'PUT',
      url: `/api/app/email-template/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}