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
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      url: `/api/app/journal-entry/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateJournalEntryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      url: '/api/app/journal-entry',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'GET',
      url: `/api/app/journal-entry/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JournalEntryDto>>({
      method: 'GET',
      url: '/api/app/journal-entry',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  post = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JournalEntryDto>({
      method: 'POST',
      url: `/api/app/journal-entry/${id}`,
    },
    { apiName: this.apiName,...config });
}