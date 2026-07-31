import type { CoaImportResultDto, CoaTemplateRowDto, ImportCoaDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ChartOfAccountsImportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getMalaysianTemplate = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CoaTemplateRowDto[]>({
      method: 'GET',
      url: '/api/app/chart-of-accounts-import/malaysian-template',
    },
    { apiName: this.apiName,...config });
  

  import = (input: ImportCoaDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CoaImportResultDto>({
      method: 'POST',
      url: '/api/app/chart-of-accounts-import/import',
      body: input,
    },
    { apiName: this.apiName,...config });
}