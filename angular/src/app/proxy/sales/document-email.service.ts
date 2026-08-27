import type { EmailPreviewDto, PreviewEmailInput, SendDeliveryNoteEmailDto, SendInvoiceEmailDto, SendPurchaseOrderEmailDto, SendQuotationEmailDto, SendSalesOrderEmailDto, SendStatementEmailDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DocumentEmailService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  previewEmail = (input: PreviewEmailInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailPreviewDto>({
      method: 'POST',
      url: '/api/app/document-email/preview-email',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sendDeliveryNoteEmail = (input: SendDeliveryNoteEmailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/document-email/send-delivery-note-email',
      body: input,
    },
    { apiName: this.apiName,...config });


  sendPurchaseOrderEmail = (input: SendPurchaseOrderEmailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/document-email/send-purchase-order-email',
      body: input,
    },
    { apiName: this.apiName,...config });


  sendQuotationEmail = (input: SendQuotationEmailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/document-email/send-quotation-email',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sendSalesInvoiceEmail = (input: SendInvoiceEmailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/document-email/send-sales-invoice-email',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sendSalesOrderEmail = (input: SendSalesOrderEmailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/document-email/send-sales-order-email',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sendStatementEmail = (input: SendStatementEmailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/document-email/send-statement-email',
      body: input,
    },
    { apiName: this.apiName,...config });
}