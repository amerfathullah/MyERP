import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';

export interface PeriodClosingVoucherDto {
  id: string;
  companyId: string;
  postingDate: string;
  closingAccountId: string;
  closingAccountName?: string;
  fiscalYearId: string;
  totalClosingAmount: number;
  status: string;
  remarks?: string;
}

export interface CreatePeriodClosingVoucherDto {
  companyId: string;
  postingDate: string;
  closingAccountId: string;
  fiscalYearId: string;
  remarks?: string;
}

@Injectable({ providedIn: 'root' })
export class PeriodClosingVoucherService {
  private rest = inject(RestService);

  getList(params?: any): Observable<{ totalCount: number; items: PeriodClosingVoucherDto[] }> {
    return this.rest.request({ method: 'GET', url: '/api/app/period-closing-voucher', params });
  }

  get(id: string): Observable<PeriodClosingVoucherDto> {
    return this.rest.request({ method: 'GET', url: `/api/app/period-closing-voucher/${id}` });
  }

  create(input: CreatePeriodClosingVoucherDto): Observable<PeriodClosingVoucherDto> {
    return this.rest.request({ method: 'POST', url: '/api/app/period-closing-voucher', body: input });
  }

  submit(id: string): Observable<PeriodClosingVoucherDto> {
    return this.rest.request({ method: 'POST', url: `/api/app/period-closing-voucher/${id}/submit` });
  }

  cancel(id: string): Observable<PeriodClosingVoucherDto> {
    return this.rest.request({ method: 'POST', url: `/api/app/period-closing-voucher/${id}/cancel` });
  }
}


