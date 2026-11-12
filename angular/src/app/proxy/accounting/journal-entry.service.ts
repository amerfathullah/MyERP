import type { CreateJournalEntryDto, JournalEntryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class JournalEntryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  amend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/journal-entry/${id}/amend`,
    },
      { apiName: this.apiName,...config });
  

  
  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/journal-entry/${id}/cancel`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateJournalEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/journal-entry',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createReversal = (sourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/journal-entry/reversal/${sourceId}`,
    },
      { apiName: this.apiName,...config });
  

  
  createReverseJournalEntry = (sourceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/journal-entry/reverse-journal-entry/${sourceId}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/journal-entry/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getJournalEntryTemplate = (documentType: string, documentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CreateJournalEntryDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/journal-entry/journal-entry-template/${documentId}`,
      params: { documentType },
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JournalEntryDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/journal-entry',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  post = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/journal-entry/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateJournalEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/journal-entry/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
