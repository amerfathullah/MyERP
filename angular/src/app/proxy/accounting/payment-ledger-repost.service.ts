import type { PaymentLedgerRepostResultDto, RepostPaymentLedgerDto, RepostPaymentLedgerForCompanyDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PaymentLedgerRepostService {
  private restService = inject(RestService);
  apiName = 'Default';


  repost = (input: RepostPaymentLedgerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentLedgerRepostResultDto>({
      method: 'POST',
      url: '/api/app/payment-ledger-repost/repost',
      body: input,
    },
    { apiName: this.apiName,...config });


  repostForCompany = (input: RepostPaymentLedgerForCompanyDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaymentLedgerRepostResultDto>({
      method: 'POST',
      url: '/api/app/payment-ledger-repost/repost-for-company',
      body: input,
    },
    { apiName: this.apiName,...config });
}
