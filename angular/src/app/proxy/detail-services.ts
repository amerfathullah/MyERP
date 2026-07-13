import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface BudgetDto {
  id?: string;
  companyId?: string;
  fiscalYearId?: string;
  budgetAgainst?: string;
  budgetAgainstId?: string;
  budgetAgainstName?: string;
  status?: number;
  accounts?: BudgetAccountDto[];
}

export interface BudgetAccountDto {
  id?: string;
  accountId?: string;
  accountName?: string;
  budgetAmount?: number;
}

export interface LandedCostVoucherDetailDto {
  id?: string;
  companyId?: string;
  voucherNumber?: string;
  postingDate?: string;
  distributionMethod?: number;
  status?: number;
  totalCharges?: number;
  totalDistributedAmount?: number;
  items?: { id?: string; description?: string; quantity?: number; amount?: number; applicableCharges?: number }[];
  charges?: { id?: string; description?: string; amount?: number }[];
}

export interface QualityInspectionDetailDto {
  id?: string;
  itemName?: string;
  inspectionType?: number;
  inspectionDate?: string;
  status?: number;
  readings?: { id?: string; specification?: string; expectedValue?: string; minValue?: number; maxValue?: number; readingValue?: string; isNumeric?: boolean; status?: number }[];
}

export interface StockReconciliationDetailDto {
  id?: string;
  reconciliationNumber?: string;
  postingDate?: string;
  status?: number;
  differenceAmount?: number;
  items?: { id?: string; itemId?: string; currentQuantity?: number; newQuantity?: number; quantityDifference?: number }[];
}

export interface HolidayListDetailDto {
  id?: string;
  name?: string;
  year?: number;
  weeklyOff?: string;
  holidays?: { id?: string; holidayDate?: string; description?: string; isWeeklyOff?: boolean }[];
}

export interface IssueDetailDto {
  id?: string;
  subject?: string;
  priority?: string;
  issueType?: string;
  description?: string;
  status?: number;
  creationTime?: string;
}

@Injectable({ providedIn: 'root' })
export class BudgetDetailService {
  private restService = inject(RestService);
  get = (id: string) => this.restService.request<any, BudgetDto>({ method: 'GET', url: `/api/app/budget/${id}` }, { apiName: 'Default' });
}

@Injectable({ providedIn: 'root' })
export class LandedCostDetailService {
  private restService = inject(RestService);
  get = (id: string) => this.restService.request<any, LandedCostVoucherDetailDto>({ method: 'GET', url: `/api/app/landed-cost-voucher/${id}` }, { apiName: 'Default' });
}

@Injectable({ providedIn: 'root' })
export class QualityInspectionDetailService {
  private restService = inject(RestService);
  get = (id: string) => this.restService.request<any, QualityInspectionDetailDto>({ method: 'GET', url: `/api/app/quality-inspection/${id}` }, { apiName: 'Default' });
}

@Injectable({ providedIn: 'root' })
export class StockReconciliationDetailService {
  private restService = inject(RestService);
  get = (id: string) => this.restService.request<any, StockReconciliationDetailDto>({ method: 'GET', url: `/api/app/stock-reconciliation/${id}` }, { apiName: 'Default' });
}

@Injectable({ providedIn: 'root' })
export class HolidayListDetailService {
  private restService = inject(RestService);
  get = (id: string) => this.restService.request<any, HolidayListDetailDto>({ method: 'GET', url: `/api/app/holiday-list/${id}` }, { apiName: 'Default' });
}

@Injectable({ providedIn: 'root' })
export class IssueDetailService {
  private restService = inject(RestService);
  get = (id: string) => this.restService.request<any, IssueDetailDto>({ method: 'GET', url: `/api/app/issue/${id}` }, { apiName: 'Default' });
  reply = (id: string) => this.restService.request<any, void>({ method: 'POST', url: `/api/app/issue/${id}/reply` }, { apiName: 'Default' });
  resolve = (id: string) => this.restService.request<any, void>({ method: 'POST', url: `/api/app/issue/${id}/resolve` }, { apiName: 'Default' });
}
