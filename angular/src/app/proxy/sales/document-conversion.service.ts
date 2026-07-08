import type { SalesOrderDto, DeliveryNoteDto, SalesInvoiceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DocumentConversionService {
  private restService = inject(RestService);
  apiName = 'Default';

  convertQuotationToSalesOrder = (quotationId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-quotation-to-sales-order/${quotationId}`,
    },
    { apiName: this.apiName, ...config });

  convertSalesOrderToDeliveryNote = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-sales-order-to-delivery-note/${salesOrderId}`,
    },
    { apiName: this.apiName, ...config });

  convertSalesOrderToSalesInvoice = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-sales-order-to-sales-invoice/${salesOrderId}`,
    },
    { apiName: this.apiName, ...config });

  convertDeliveryNoteToSalesInvoice = (deliveryNoteId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-delivery-note-to-sales-invoice/${deliveryNoteId}`,
    },
    { apiName: this.apiName, ...config });
}
