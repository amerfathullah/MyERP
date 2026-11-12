import type { CreateUpdateCustomerDto, CustomerDto, CustomerOverviewDto, CustomerTransactionDto, GetCustomerListDto, GetCustomerOverviewInputDto, GetCustomerTransactionsInputDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CustomerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  create = (input: CreateUpdateCustomerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/customer',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/customer/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/customer/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getCustomerCompanies = (customerId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/customer/customer-companies/${customerId}`,
    },
      { apiName: this.apiName,...config });
  

  
  getCustomerOverview = (input: GetCustomerOverviewInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerOverviewDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/customer/customer-overview',
      params: { customerId: input.customerId, companyId: input.companyId, period: input.period },
    },
      { apiName: this.apiName,...config });
  

  
  getCustomerTransactions = (input: GetCustomerTransactionsInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerTransactionDto[]>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/customer/customer-transactions',
      params: { customerId: input.customerId, companyId: input.companyId, docType: input.docType, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetCustomerListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CustomerDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/customer',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateUpdateCustomerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/customer/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
