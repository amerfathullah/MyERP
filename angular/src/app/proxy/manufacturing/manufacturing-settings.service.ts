import type { ManufacturingSettingsDto, SaveManufacturingSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ManufacturingSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getForCompany = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ManufacturingSettingsDto>({
      method: 'GET',
      url: `/api/app/manufacturing-settings/for-company/${companyId}`,
    },
    { apiName: this.apiName,...config });
  

  save = (input: SaveManufacturingSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ManufacturingSettingsDto>({
      method: 'POST',
      url: '/api/app/manufacturing-settings/save',
      body: input,
    },
    { apiName: this.apiName,...config });
}