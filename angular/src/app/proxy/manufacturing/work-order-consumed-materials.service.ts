import type { WorkOrderConsumedMaterialsReportDto, WorkOrderConsumedMaterialsRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WorkOrderConsumedMaterialsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: WorkOrderConsumedMaterialsRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderConsumedMaterialsReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/work-order-consumed-materials/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, workOrderId: input.workOrderId, productionItemId: input.productionItemId, status: input.status, showExtraConsumedMaterialsOnly: input.showExtraConsumedMaterialsOnly },
    },
      { apiName: this.apiName,...config });
}
