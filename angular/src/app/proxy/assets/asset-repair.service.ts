import type { AssetRepairDto, AssetRepairExpenseAccountDto, CreateUpdateAssetRepairDto, GetAssetRepairConsumedItemRateInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetRepairService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/asset-repair/${id}/cancel`,
    },
      { apiName: this.apiName,...config });
  

  
  complete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/asset-repair/${id}/complete`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateUpdateAssetRepairDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/asset-repair',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/asset-repair/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/asset-repair/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AssetRepairDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/asset-repair',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });


  getExpenseAccountsForPurchaseInvoice = (purchaseInvoiceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairExpenseAccountDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/asset-repair/expense-accounts-for-purchase-invoice',
      params: { purchaseInvoiceId },
    },
      { apiName: this.apiName,...config });


  getConsumedItemValuationRate = (input: GetAssetRepairConsumedItemRateInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/asset-repair/consumed-item-valuation-rate',
      params: {
        itemId: input.itemId,
        warehouseId: input.warehouseId,
        qty: input.qty,
        postingDate: input.postingDate,
        serialAndBatchBundleId: input.serialAndBatchBundleId,
      },
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateUpdateAssetRepairDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetRepairDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/asset-repair/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
