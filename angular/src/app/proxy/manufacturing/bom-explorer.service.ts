import type { BomExplorerDto, BomExplorerRequestDto, BomLookupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BomExplorerService {
  private restService = inject(RestService);
  apiName = 'Default';

  getExplorer = (input: BomExplorerRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomExplorerDto>({
      method: 'POST',
      url: '/api/app/bom-explorer/explorer',
      body: input,
    },
    { apiName: this.apiName, ...config });

  getBomList = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomLookupDto[]>({
      method: 'GET',
      url: `/api/app/bom-explorer/bom-list`,
      params: { companyId },
    },
    { apiName: this.apiName, ...config });
}
