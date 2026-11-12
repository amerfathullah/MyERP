import type { SupplierLedgerSummaryReportDto, SupplierLedgerSummaryRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SupplierLedgerSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: SupplierLedgerSummaryRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierLedgerSummaryReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/supplier-ledger-summary/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, supplierId: input.supplierId, supplierGroupId: input.supplierGroupId },
    },
      { apiName: this.apiName,...config });
}
