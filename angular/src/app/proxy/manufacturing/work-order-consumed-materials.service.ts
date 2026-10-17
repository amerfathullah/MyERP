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
      method: 'POST',
      url: '/api/app/work-order-consumed-materials/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
