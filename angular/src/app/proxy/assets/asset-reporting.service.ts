import type { AssetDepreciationsAndBalancesReportDto, AssetDepreciationsAndBalancesRequestDto, FixedAssetRegisterReportDto, FixedAssetRegisterRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AssetReportingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getAssetDepreciationsAndBalances = (input: AssetDepreciationsAndBalancesRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AssetDepreciationsAndBalancesReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/asset-reporting/asset-depreciations-and-balances',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, groupBy: input.groupBy, assetCategoryId: input.assetCategoryId, assetId: input.assetId, financeBookId: input.financeBookId },
    },
      { apiName: this.apiName,...config });
  

  
  getFixedAssetRegister = (input: FixedAssetRegisterRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FixedAssetRegisterReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/asset-reporting/fixed-asset-register',
      params: { companyId: input.companyId, status: input.status, assetCategoryId: input.assetCategoryId, financeBookId: input.financeBookId, includeDefaultBookAssets: input.includeDefaultBookAssets, onlyExistingAssets: input.onlyExistingAssets, filterBasedOn: input.filterBasedOn, dateBasedOn: input.dateBasedOn, fromDate: input.fromDate, toDate: input.toDate, locationId: input.locationId, costCenterId: input.costCenterId, groupBy: input.groupBy },
    },
      { apiName: this.apiName,...config });
}
