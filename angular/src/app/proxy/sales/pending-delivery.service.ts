import type { CreateDeliveryNoteResultDto, CreateDnFromPendingDto, PendingDeliveryReportDto, PendingDeliveryRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PendingDeliveryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createDeliveryNote = (input: CreateDnFromPendingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CreateDeliveryNoteResultDto>({
      method: 'POST',
      url: '/api/app/pending-delivery/delivery-note',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getReport = (input: PendingDeliveryRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PendingDeliveryReportDto>({
      method: 'GET',
      url: '/api/app/pending-delivery/report',
      params: { companyId: input.companyId, asOfDate: input.asOfDate, customerId: input.customerId, itemId: input.itemId, overdueOnly: input.overdueOnly },
    },
    { apiName: this.apiName,...config });
}