import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface PricingRuleDto {
  id?: string;
  title?: string;
  applicableFor?: string;
  applyOn?: number;
  applyOnId?: string;
  applyOnName?: string;
  ruleType?: number;
  discountPercentage?: number;
  discountAmount?: number;
  rate?: number;
  minQty?: number;
  maxQty?: number;
  priority?: number;
  validFrom?: string;
  validUpto?: string;
  isDisabled?: boolean;
}

export interface WorkstationDto {
  id?: string;
  companyId?: string;
  name?: string;
  workstationType?: string;
  productionCapacity?: number;
  hourRate?: number;
  isActive?: boolean;
}

export interface SubcontractingOrderDto {
  id?: string;
  orderNumber?: string;
  supplierName?: string;
  transactionDate?: string;
  totalQty?: number;
  status?: number;
}

@Injectable({ providedIn: 'root' })
export class PricingRuleService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PricingRuleDto>>({
      method: 'GET', url: '/api/app/pricing-rule',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({ method: 'DELETE', url: `/api/app/pricing-rule/${id}` }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class WorkstationProxyService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WorkstationDto>>({
      method: 'GET', url: '/api/app/manufacturing/workstations',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class SubcontractingService {
  private restService = inject(RestService);
  apiName = 'Default';

  getOrderList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SubcontractingOrderDto>>({
      method: 'GET', url: '/api/app/subcontracting/orders',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}
