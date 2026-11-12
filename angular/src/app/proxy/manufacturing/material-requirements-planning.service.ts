import type { CreateOrdersFromMrpInput, MaterialRequirementsPlanningFilterDto, MaterialRequirementsPlanningReportDto, MrpOrdersCreatedDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MaterialRequirementsPlanningService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  createOrders = (input: CreateOrdersFromMrpInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MrpOrdersCreatedDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/material-requirements-planning/orders',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  getReport = (input: MaterialRequirementsPlanningFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequirementsPlanningReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/material-requirements-planning/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, bucketSize: input.bucketSize, itemId: input.itemId, warehouseId: input.warehouseId, includeSafetyStock: input.includeSafetyStock },
    },
      { apiName: this.apiName,...config });
}
