import type { ItemWiseSalesRegisterFilterDto, ItemWiseSalesRegisterLineDto, RegisterFilterDto, RegisterReportDto, SalesRegisterLineDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesRegisterService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getItemWiseReport = (input: ItemWiseSalesRegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RegisterReportDto<ItemWiseSalesRegisterLineDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-register/item-wise-report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, customerId: input.customerId, customerGroupId: input.customerGroupId, itemId: input.itemId, itemCode: input.itemCode, itemGroupId: input.itemGroupId },
    },
      { apiName: this.apiName,...config });
  

  
  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RegisterReportDto<SalesRegisterLineDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-register/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, customerId: input.customerId, customerGroupId: input.customerGroupId, supplierId: input.supplierId, supplierGroupId: input.supplierGroupId, includePayments: input.includePayments },
    },
      { apiName: this.apiName,...config });
}
