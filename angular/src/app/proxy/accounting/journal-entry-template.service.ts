import type { CreateUpdateJournalEntryTemplateDto, GetJournalEntryTemplateListDto, JournalEntryTemplateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class JournalEntryTemplateService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateJournalEntryTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryTemplateDto>({
      method: 'POST',
      url: '/api/app/journal-entry-template',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/journal-entry-template/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryTemplateDto>({
      method: 'GET',
      url: `/api/app/journal-entry-template/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetJournalEntryTemplateListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JournalEntryTemplateDto>>({
      method: 'GET',
      url: '/api/app/journal-entry-template',
      params: { companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdateJournalEntryTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryTemplateDto>({
      method: 'PUT',
      url: `/api/app/journal-entry-template/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
