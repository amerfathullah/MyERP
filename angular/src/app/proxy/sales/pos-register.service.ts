import type { PosRegisterDto, PosRegisterRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PosRegisterService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: PosRegisterRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosRegisterDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/pos-register/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, posProfileId: input.posProfileId, cashierId: input.cashierId, customerId: input.customerId, isReturn: input.isReturn, modeOfPayment: input.modeOfPayment, groupBy: input.groupBy },
    },
      { apiName: this.apiName,...config });
}
