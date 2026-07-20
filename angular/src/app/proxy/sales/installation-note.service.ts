import type { CreateInstallationNoteDto, InstallationNoteDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class InstallationNoteService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/installation-note/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateInstallationNoteDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InstallationNoteDto>({
      method: 'POST',
      url: '/api/app/installation-note',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InstallationNoteDto>({
      method: 'GET',
      url: `/api/app/installation-note/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InstallationNoteDto>>({
      method: 'GET',
      url: '/api/app/installation-note',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/installation-note/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}