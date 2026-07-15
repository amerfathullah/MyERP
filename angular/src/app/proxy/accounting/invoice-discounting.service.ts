import type { CreateInvoiceDiscountingDto, InvoiceDiscountingDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InvoiceDiscountingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  calculate = (input: CreateInvoiceDiscountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting/calculate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  disburse = (input: CreateInvoiceDiscountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting/disburse',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  settle = (companyId: string, amount: number, shortTermLoanAccountId: string, receivableAccountId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting/settle',
      params: { companyId, amount, shortTermLoanAccountId, receivableAccountId },
    },
    { apiName: this.apiName,...config });
}