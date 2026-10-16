import type {
  GetPurchaseOrderAnalysisDto,
  PurchaseOrderAnalysisReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PurchaseOrderAnalysisService {
  private restService = inject(RestService);
  apiName = 'Default';

  getAnalysis = (input: GetPurchaseOrderAnalysisDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderAnalysisReportDto>(
      {
        method: 'GET',
        url: '/api/app/purchase-order-analysis/analysis',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          supplierId: input.supplierId,
          purchaseOrderId: input.purchaseOrderId,
          itemId: input.itemId,
          projectId: input.projectId,
          groupByPo: input.groupByPo,
          groupByItem: input.groupByItem,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
