import type {
  ProcurementTrackerFilterDto,
  ProcurementTrackerReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProcurementTrackerService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ProcurementTrackerFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcurementTrackerReportDto>(
      {
        method: 'GET',
        url: '/api/app/procurement-tracker/report',
        params: {
          companyId: input.companyId,
          costCenterId: input.costCenterId,
          projectId: input.projectId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          showCompletedOrders: input.showCompletedOrders,
        },
      },
      { apiName: this.apiName, ...config }
     );
}
