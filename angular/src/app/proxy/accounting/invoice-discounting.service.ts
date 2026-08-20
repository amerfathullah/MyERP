import type {
  CalculateDiscountingDto,
  CreateInvoiceDiscountingDto,
  DisburseInvoiceDiscountingDto,
  DiscountingCalculationResultDto,
  InvoiceDiscountingDto,
  InvoiceForDiscountingDto,
  SubmitInvoiceDiscountingDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InvoiceDiscountingService {
  private restService = inject(RestService);
  apiName = 'Default';


  getList = (input: PagedAndSortedResultRequestDto, companyId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InvoiceDiscountingDto>>({
      method: 'GET',
      url: '/api/app/invoice-discounting',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, companyId },
    },
    { apiName: this.apiName, ...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'GET',
      url: `/api/app/invoice-discounting/${id}`,
    },
    { apiName: this.apiName, ...config });


  getEligibleInvoices = (companyId: string, customerId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceForDiscountingDto[]>({
      method: 'GET',
      url: '/api/app/invoice-discounting/eligible-invoices',
      params: { companyId, customerId },
    },
    { apiName: this.apiName, ...config });


  calculate = (input: CalculateDiscountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DiscountingCalculationResultDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting/calculate',
      body: input,
    },
    { apiName: this.apiName, ...config });


  create = (input: CreateInvoiceDiscountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: '/api/app/invoice-discounting',
      body: input,
    },
    { apiName: this.apiName, ...config });


  submit = (id: string, input: SubmitInvoiceDiscountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: `/api/app/invoice-discounting/${id}/submit`,
      body: input,
    },
    { apiName: this.apiName, ...config });


  disburse = (id: string, input: DisburseInvoiceDiscountingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: `/api/app/invoice-discounting/${id}/disburse`,
      body: input,
    },
    { apiName: this.apiName, ...config });


  settle = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: `/api/app/invoice-discounting/${id}/settle`,
    },
    { apiName: this.apiName, ...config });


  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDiscountingDto>({
      method: 'POST',
      url: `/api/app/invoice-discounting/${id}/cancel`,
    },
    { apiName: this.apiName, ...config });
}
