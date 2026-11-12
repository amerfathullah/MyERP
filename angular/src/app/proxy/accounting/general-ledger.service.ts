import type { GeneralLedgerFilterDto, GeneralLedgerReportDto, VoucherLedgerDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class GeneralLedgerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getForVoucher = (voucherType: string, voucherId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VoucherLedgerDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/general-ledger/for-voucher/${voucherId}`,
      params: { voucherType },
    },
      { apiName: this.apiName,...config });
  

  
  getReport = (input: GeneralLedgerFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GeneralLedgerReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/general-ledger/report',
      params: { companyId: input.companyId, accountId: input.accountId, fromDate: input.fromDate, toDate: input.toDate, partyType: input.partyType, partyId: input.partyId, voucherNumber: input.voucherNumber, costCenterId: input.costCenterId, projectId: input.projectId, includeUnassignedCostCenter: input.includeUnassignedCostCenter, includeUnassignedProject: input.includeUnassignedProject, convertAtReportingDateRate: input.convertAtReportingDateRate, presentationCurrency: input.presentationCurrency, showAmountInCompanyCurrency: input.showAmountInCompanyCurrency },
    },
      { apiName: this.apiName,...config });
}
