import type { BatchStatementOfAccountsInput, BatchStatementOfAccountsResultDto, StatementOfAccountsDto, SupplierStatementDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StatementOfAccountsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getCustomerStatement = (customerId: string, companyId: string, fromDate: string, toDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StatementOfAccountsDto>({
      method: 'GET',
      url: '/api/app/statement-of-accounts/customer-statement',
      params: { customerId, companyId, fromDate, toDate },
    },
    { apiName: this.apiName,...config });
  

  getSupplierStatement = (supplierId: string, companyId: string, fromDate: string, toDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierStatementDto>({
      method: 'GET',
      url: '/api/app/statement-of-accounts/supplier-statement',
      params: { supplierId, companyId, fromDate, toDate },
    },
    { apiName: this.apiName,...config });
  

  processBatchStatement = (input: BatchStatementOfAccountsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BatchStatementOfAccountsResultDto>({
      method: 'POST',
      url: '/api/app/statement-of-accounts/process-batch-statement',
      body: input,
    },
    { apiName: this.apiName,...config });
}