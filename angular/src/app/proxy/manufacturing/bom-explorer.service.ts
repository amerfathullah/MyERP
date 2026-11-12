import type { BomExplorerDto, BomExplorerRequestDto, BomLookupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BomExplorerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getBomList = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomLookupDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/bom-explorer/bom-list/${companyId}`,
    },
      { apiName: this.apiName,...config });
  

  
  getExplorer = (input: BomExplorerRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomExplorerDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/bom-explorer/explorer',
      params: { companyId: input.companyId, bomId: input.bomId, quantity: input.quantity, warehouseId: input.warehouseId },
    },
      { apiName: this.apiName,...config });
}
