import type { CreateUpdatePrintFormatDto, PrintFormatDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PrintFormatService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdatePrintFormatDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PrintFormatDto>({
      method: 'POST',
      url: '/api/app/print-format',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/print-format/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PrintFormatDto>({
      method: 'GET',
      url: `/api/app/print-format/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PrintFormatDto>>({
      method: 'GET',
      url: '/api/app/print-format',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdatePrintFormatDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PrintFormatDto>({
      method: 'PUT',
      url: `/api/app/print-format/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}