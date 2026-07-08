import type { ExportRequestDto, ExportResultDto, ImportJobDto, StartImportDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ImportExportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  export = (input: ExportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExportResultDto>({
      method: 'POST',
      url: '/api/app/import-export/export',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getImportHistory = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ImportJobDto>>({
      method: 'GET',
      url: '/api/app/import-export/import-history',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getImportStatus = (jobId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ImportJobDto>({
      method: 'GET',
      url: `/api/app/import-export/import-status/${jobId}`,
    },
    { apiName: this.apiName,...config });
  

  startImport = (input: StartImportDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ImportJobDto>({
      method: 'POST',
      url: '/api/app/import-export/start-import',
      body: input,
    },
    { apiName: this.apiName,...config });
}