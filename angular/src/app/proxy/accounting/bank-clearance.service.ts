import type { BankClearanceEntryDto, BulkClearanceResultDto, GetBankClearanceEntriesInput, SetClearanceDateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankClearanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getEntries = (input: GetBankClearanceEntriesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankClearanceEntryDto[]>({
      method: 'GET',
      url: '/api/app/bank-clearance/entries',
      params: { bankAccountId: input.bankAccountId, companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, includeCleared: input.includeCleared },
    },
    { apiName: this.apiName,...config });
  

  setClearanceDate = (input: SetClearanceDateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BulkClearanceResultDto>({
      method: 'POST',
      url: '/api/app/bank-clearance/set-clearance-date',
      body: input,
    },
    { apiName: this.apiName,...config });
}