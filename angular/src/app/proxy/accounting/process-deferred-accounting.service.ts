import type {
  ProcessDeferredAccountingDto,
  ProcessDeferredAccountingGetListInput,
  CreateProcessDeferredAccountingDto,
  UpdateProcessDeferredAccountingDto
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProcessDeferredAccountingService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: ProcessDeferredAccountingGetListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProcessDeferredAccountingDto>>({
      method: 'GET',
      url: '/api/app/process-deferred-accounting',
      params: {
        filter: input.filter,
        companyId: input.companyId,
        type: input.type,
        fromDate: input.fromDate,
        toDate: input.toDate,
        sorting: input.sorting,
        skipCount: input.skipCount,
        maxResultCount: input.maxResultCount,
      },
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessDeferredAccountingDto>({
      method: 'GET',
      url: `/api/app/process-deferred-accounting/${id}`,
    },
    { apiName: this.apiName, ...config });

  create = (input: CreateProcessDeferredAccountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessDeferredAccountingDto>({
      method: 'POST',
      url: '/api/app/process-deferred-accounting',
      body: input,
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: UpdateProcessDeferredAccountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessDeferredAccountingDto>({
      method: 'PUT',
      url: `/api/app/process-deferred-accounting/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/process-deferred-accounting/${id}`,
    },
    { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessDeferredAccountingDto>({
      method: 'POST',
      url: `/api/app/process-deferred-accounting/${id}/submit`,
    },
    { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessDeferredAccountingDto>({
      method: 'POST',
      url: `/api/app/process-deferred-accounting/${id}/cancel`,
    },
    { apiName: this.apiName, ...config });
}
