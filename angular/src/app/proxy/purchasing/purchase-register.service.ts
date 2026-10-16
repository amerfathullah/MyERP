import type { ItemWisePurchaseRegisterLineDto, ItemWiseRegisterFilterDto, PurchaseRegisterLineDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { RegisterFilterDto, RegisterReportDto } from '../sales/models';

@Injectable({
  providedIn: 'root',
})
export class PurchaseRegisterService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RegisterReportDto<PurchaseRegisterLineDto>>({
      method: 'GET',
      url: '/api/app/purchase-register/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });

  getItemWiseReport = (input: ItemWiseRegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RegisterReportDto<ItemWisePurchaseRegisterLineDto>>({
      method: 'GET',
      url: '/api/app/purchase-register/item-wise-report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        supplierId: input.supplierId,
        supplierGroupId: input.supplierGroupId,
        itemId: input.itemId,
        itemCode: input.itemCode,
        itemGroupId: input.itemGroupId,
      },
    },
    { apiName: this.apiName, ...config });
}