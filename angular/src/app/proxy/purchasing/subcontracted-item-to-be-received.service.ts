import type {
  SubcontractedItemToBeReceivedFilterDto,
  SubcontractedItemToBeReceivedReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubcontractedItemToBeReceivedService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SubcontractedItemToBeReceivedFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractedItemToBeReceivedReportDto>(
      {
        method: 'GET',
        url: '/api/app/subcontracted-item-to-be-received/report',
        params: {
          companyId: input.companyId,
          supplierId: input.supplierId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
