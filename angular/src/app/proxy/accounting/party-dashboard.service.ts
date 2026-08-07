import type { PartyDashboardDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PartyDashboardService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getCustomerDashboard = (customerId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyDashboardDto>({
      method: 'GET',
      url: `/api/app/party-dashboard/customer-dashboard/${customerId}`,
    },
    { apiName: this.apiName,...config });
  

  getSupplierDashboard = (supplierId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyDashboardDto>({
      method: 'GET',
      url: `/api/app/party-dashboard/supplier-dashboard/${supplierId}`,
    },
    { apiName: this.apiName,...config });
}