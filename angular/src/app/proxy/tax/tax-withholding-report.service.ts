import type {
  TaxWithholdingDetailsReportDto,
  TaxWithholdingReportFilterDto,
  TdsComputationSummaryReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TaxWithholdingReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getDetails = (input: TaxWithholdingReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxWithholdingDetailsReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/tax-withholding-report/details',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          partyType: input.partyType,
          partyId: input.partyId,
          taxCategory: input.taxCategory,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getTdsComputationSummary = (input: TaxWithholdingReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TdsComputationSummaryReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/tax-withholding-report/tds-computation-summary',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          partyType: input.partyType,
          partyId: input.partyId,
          taxCategory: input.taxCategory,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
