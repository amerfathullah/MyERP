import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface JobCardDto {
  id?: string;
  companyId?: string;
  workOrderId?: string;
  operationId?: string;
  workstationId?: string;
  forQuantity?: number;
  completedQty?: number;
  totalTimeInMins?: number;
  plannedTimeInMins?: number;
  sequenceId?: number;
  status?: number;
  startedAt?: string;
  completedAt?: string;
  timeLogs?: JobCardTimeLogDto[];
}

export interface JobCardTimeLogDto {
  id?: string;
  fromTime?: string;
  toTime?: string;
  timeInMins?: number;
  completedQty?: number;
}

export interface SubscriptionDto {
  id?: string;
  companyId?: string;
  partyId?: string;
  partyType?: string;
  partyName?: string;
  subscriptionNumber?: string;
  billingInterval?: string;
  billingIntervalCount?: number;
  startDate?: string;
  endDate?: string;
  currentInvoiceStart?: string;
  currentInvoiceEnd?: string;
  totalPerInterval?: number;
  status?: number;
  plans?: SubscriptionPlanDto[];
}

export interface SubscriptionPlanDto {
  id?: string;
  itemId?: string;
  itemName?: string;
  qty?: number;
  rate?: number;
}

export interface DunningDto {
  id?: string;
  companyId?: string;
  customerId?: string;
  customerName?: string;
  postingDate?: string;
  dunningLevel?: number;
  totalOutstanding?: number;
  dunningFee?: number;
  interestAmount?: number;
  grandTotal?: number;
  status?: number;
  overduePaymentCount?: number;
}

@Injectable({ providedIn: 'root' })
export class JobCardService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JobCardDto>>({
      method: 'GET', url: '/api/app/job-card',
      params: { workOrderId: input.workOrderId, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({ method: 'GET', url: `/api/app/job-card/${id}` }, { apiName: this.apiName, ...config });

  start = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({ method: 'POST', url: `/api/app/job-card/${id}/start` }, { apiName: this.apiName, ...config });

  complete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({ method: 'POST', url: `/api/app/job-card/${id}/complete` }, { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardDto>({ method: 'POST', url: `/api/app/job-card/${id}/cancel` }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SubscriptionDto>>({
      method: 'GET', url: '/api/app/subscription',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionDto>({ method: 'GET', url: `/api/app/subscription/${id}` }, { apiName: this.apiName, ...config });

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionDto>({ method: 'POST', url: `/api/app/subscription/${id}/cancel` }, { apiName: this.apiName, ...config });

  advancePeriod = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionDto>({ method: 'POST', url: `/api/app/subscription/${id}/advance-period` }, { apiName: this.apiName, ...config });

  generateInvoice = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, any>({ method: 'POST', url: `/api/app/subscription/${id}/generate-invoice` }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class DunningService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DunningDto>>({
      method: 'GET', url: '/api/app/dunning',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningDto>({ method: 'GET', url: `/api/app/dunning/${id}` }, { apiName: this.apiName, ...config });

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningDto>({ method: 'POST', url: `/api/app/dunning/${id}/submit` }, { apiName: this.apiName, ...config });

  resolve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DunningDto>({ method: 'POST', url: `/api/app/dunning/${id}/resolve` }, { apiName: this.apiName, ...config });
}
