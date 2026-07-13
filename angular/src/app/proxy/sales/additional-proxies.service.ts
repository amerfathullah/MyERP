import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface BlanketOrderDto {
  id?: string;
  companyId?: string;
  orderNumber?: string;
  orderType?: string;
  partyId?: string;
  partyName?: string;
  fromDate?: string;
  toDate?: string;
  status?: number;
  items?: BlanketOrderItemDto[];
}

export interface BlanketOrderItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  qty?: number;
  rate?: number;
  orderedQty?: number;
  remainingQty?: number;
}

export interface ExpenseClaimDto {
  id?: string;
  companyId?: string;
  employeeId?: string;
  employeeName?: string;
  postingDate?: string;
  expenseType?: string;
  totalClaimedAmount?: number;
  totalSanctionedAmount?: number;
  status?: number;
  expenses?: ExpenseClaimDetailDto[];
}

export interface ExpenseClaimDetailDto {
  id?: string;
  expenseDate?: string;
  description?: string;
  amount?: number;
}

export interface SupplierQuotationDto {
  id?: string;
  companyId?: string;
  supplierId?: string;
  supplierName?: string;
  quotationNumber?: string;
  transactionDate?: string;
  validTill?: string;
  currency?: string;
  netTotal?: number;
  grandTotal?: number;
  status?: number;
  items?: SupplierQuotationItemDto[];
}

export interface SupplierQuotationItemDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  qty?: number;
  rate?: number;
  amount?: number;
}

@Injectable({ providedIn: 'root' })
export class BlanketOrderService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BlanketOrderDto>>({
      method: 'GET', url: '/api/app/blanket-order',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlanketOrderDto>({ method: 'GET', url: `/api/app/blanket-order/${id}` }, { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlanketOrderDto>({ method: 'POST', url: `/api/app/blanket-order/${id}/submit` }, { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlanketOrderDto>({ method: 'POST', url: `/api/app/blanket-order/${id}/cancel` }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class ExpenseClaimService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ExpenseClaimDto>>({
      method: 'GET', url: '/api/app/expense-claim',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpenseClaimDto>({ method: 'GET', url: `/api/app/expense-claim/${id}` }, { apiName: this.apiName, ...config });

  approve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpenseClaimDto>({ method: 'POST', url: `/api/app/expense-claim/${id}/approve` }, { apiName: this.apiName, ...config });

  reject = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExpenseClaimDto>({ method: 'POST', url: `/api/app/expense-claim/${id}/reject` }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class SupplierQuotationService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SupplierQuotationDto>>({
      method: 'GET', url: '/api/app/supplier-quotation',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationDto>({ method: 'GET', url: `/api/app/supplier-quotation/${id}` }, { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationDto>({ method: 'POST', url: `/api/app/supplier-quotation/${id}/submit` }, { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierQuotationDto>({ method: 'POST', url: `/api/app/supplier-quotation/${id}/cancel` }, { apiName: this.apiName, ...config });
}
