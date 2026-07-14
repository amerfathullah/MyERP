import type { RegisterFilterDto, RegisterReportDto, SalesRegisterLineDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesRegisterService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RegisterReportDto<SalesRegisterLineDto>>({
      method: 'GET',
      url: '/api/app/sales-register/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });
}