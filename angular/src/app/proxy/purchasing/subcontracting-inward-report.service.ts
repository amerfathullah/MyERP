import type {
  SubcontractedItemsToBeDeliveredReportDto,
  SubcontractingInwardOrderSummaryReportDto,
  SubcontractedRawMaterialsToBeReceivedReportDto,
  SubcontractingInwardReportFilterDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SubcontractingInwardReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getItemsToBeDeliveredReport = (input: SubcontractingInwardReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractedItemsToBeDeliveredReportDto>(
      {
        method: 'GET',
        url: '/api/app/subcontracting-inward-report/items-to-be-delivered-report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          subcontractingInwardOrderId: input.subcontractingInwardOrderId,
          supplierId: input.supplierId,
          itemId: input.itemId,
          status: input.status,
        },
      },
      { apiName: this.apiName, ...config }
    );

  getOrderSummaryReport = (input: SubcontractingInwardReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractingInwardOrderSummaryReportDto>(
      {
        method: 'GET',
        url: '/api/app/subcontracting-inward-report/order-summary-report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          subcontractingInwardOrderId: input.subcontractingInwardOrderId,
          supplierId: input.supplierId,
          itemId: input.itemId,
          status: input.status,
        },
      },
      { apiName: this.apiName, ...config }
    );

  getRawMaterialsToBeReceivedReport = (input: SubcontractingInwardReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubcontractedRawMaterialsToBeReceivedReportDto>(
      {
        method: 'GET',
        url: '/api/app/subcontracting-inward-report/raw-materials-to-be-received-report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          subcontractingInwardOrderId: input.subcontractingInwardOrderId,
          supplierId: input.supplierId,
          itemId: input.itemId,
          status: input.status,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
