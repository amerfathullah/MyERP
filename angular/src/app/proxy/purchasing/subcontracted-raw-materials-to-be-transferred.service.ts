import type {
  SubcontractedRawMaterialsToBeTransferredFilterDto,
  SubcontractedRawMaterialsToBeTransferredReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubcontractedRawMaterialsToBeTransferredService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SubcontractedRawMaterialsToBeTransferredFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractedRawMaterialsToBeTransferredReportDto>(
      {
        method: 'GET',
        url: '/api/app/subcontracted-raw-materials-to-be-transferred/report',
        params: {
          companyId: input.companyId,
          supplierId: input.supplierId,
          subcontractOrderId: input.subcontractOrderId,
          rawMaterialItemId: input.rawMaterialItemId,
          fromDate: input.fromDate,
          toDate: input.toDate,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
