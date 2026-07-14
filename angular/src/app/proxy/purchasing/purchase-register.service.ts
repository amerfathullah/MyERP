import type { PurchaseRegisterLineDto } from './models';
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
}