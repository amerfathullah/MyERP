import type { DocumentPrintResult } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DocumentPrintService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getDeliveryNotePrint = (deliveryNoteId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentPrintResult>({
      method: 'GET',
      url: `/api/app/document-print/delivery-note-print/${deliveryNoteId}`,
    },
    { apiName: this.apiName,...config });
  

  getPurchaseOrderPrint = (orderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentPrintResult>({
      method: 'GET',
      url: `/api/app/document-print/purchase-order-print/${orderId}`,
    },
    { apiName: this.apiName,...config });
  

  getQuotationPrint = (quotationId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentPrintResult>({
      method: 'GET',
      url: `/api/app/document-print/quotation-print/${quotationId}`,
    },
    { apiName: this.apiName,...config });
  

  getSalesInvoicePrint = (invoiceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentPrintResult>({
      method: 'GET',
      url: `/api/app/document-print/sales-invoice-print/${invoiceId}`,
    },
    { apiName: this.apiName,...config });


  getSalesOrderPrint = (orderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentPrintResult>({
      method: 'GET',
      url: `/api/app/document-print/sales-order-print/${orderId}`,
    },
    { apiName: this.apiName,...config });
}