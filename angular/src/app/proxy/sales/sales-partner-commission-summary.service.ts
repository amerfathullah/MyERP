import type {
  GetSalesPartnerCommissionSummaryInput,
  SalesPartnerCommissionSummaryReportDto,
  SalesPartnerTransactionSummaryReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesPartnerCommissionSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: GetSalesPartnerCommissionSummaryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerCommissionSummaryReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-partner-commission-summary/report',
      params: {
        companyId: input.companyId,
        docType: input.docType,
        fromDate: input.fromDate,
        toDate: input.toDate,
        salesPartnerId: input.salesPartnerId,
        customerId: input.customerId,
        territoryId: input.territoryId,
        itemGroupId: input.itemGroupId,
        brand: input.brand,
        showReturnEntries: input.showReturnEntries,
      },
    },
      { apiName: this.apiName, ...config });

  getTransactionSummary = (input: GetSalesPartnerCommissionSummaryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesPartnerTransactionSummaryReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/sales-partner-commission-summary/transaction-summary',
      params: {
        companyId: input.companyId,
        docType: input.docType,
        fromDate: input.fromDate,
        toDate: input.toDate,
        salesPartnerId: input.salesPartnerId,
        customerId: input.customerId,
        territoryId: input.territoryId,
        itemGroupId: input.itemGroupId,
        brand: input.brand,
        showReturnEntries: input.showReturnEntries,
      },
    },
      { apiName: this.apiName, ...config });
}
