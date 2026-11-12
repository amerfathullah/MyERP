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
      headers: { Accept: 'application/json' },
      url: '/api/app/chart-of-accounts-import/malaysian-template',
    },
      { apiName: this.apiName,...config });
  

  
  getTemplateByCountry = (countryCode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CoaTemplateRowDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/chart-of-accounts-import/template-by-country',
      params: { countryCode },
    },
      { apiName: this.apiName,...config });
  

  
  getUsTemplate = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CoaTemplateRowDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/chart-of-accounts-import/us-template',
    },
      { apiName: this.apiName,...config });
  

  
  import = (input: ImportCoaDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CoaImportResultDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/chart-of-accounts-import/import',
      body: input,
    },
      { apiName: this.apiName,...config });
}
