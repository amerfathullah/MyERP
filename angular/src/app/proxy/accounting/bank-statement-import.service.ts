import type { BankStatementImportInput, BankStatementImportResult, Mt940ImportInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BankStatementImportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  importFromCsv = (input: BankStatementImportInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankStatementImportResult>({
      method: 'POST',
      url: '/api/app/bank-statement-import/import-from-csv',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  importFromMt940 = (input: Mt940ImportInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BankStatementImportResult>({
      method: 'POST',
      url: '/api/app/bank-statement-import/import-from-mt940',
      body: input,
    },
    { apiName: this.apiName,...config });
}