import type { CompanyRestrictionDto, SaveCompanyRestrictionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CompanyRestrictionService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (parentType: string, parentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompanyRestrictionDto>({
      method: 'GET',
      url: '/api/app/company-restriction',
      params: { parentType, parentId },
    },
    { apiName: this.apiName,...config });
  

  save = (input: SaveCompanyRestrictionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/company-restriction/save',
      body: input,
    },
    { apiName: this.apiName,...config });
}