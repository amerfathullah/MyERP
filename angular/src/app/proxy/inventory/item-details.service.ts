import type { GetItemDetailsInput, ItemDetailsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemDetailsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getItemDetails = (input: GetItemDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemDetailsDto>({
      method: 'GET',
      url: '/api/app/item-details/item-details',
      params: { itemId: input.itemId, transactionType: input.transactionType, warehouseId: input.warehouseId, companyId: input.companyId },
    },
    { apiName: this.apiName,...config });
}