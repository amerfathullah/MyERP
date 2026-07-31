import type { DeliveryNoteDto, PartialDeliveryItemDto, QuotationDto, SalesInvoiceDto, SalesOrderDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DocumentConversionService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  convertDeliveryNoteToSalesInvoice = (deliveryNoteId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-delivery-note-to-sales-invoice/${deliveryNoteId}`,
    },
    { apiName: this.apiName,...config });
  

  convertOpportunityToQuotation = (opportunityId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-opportunity-to-quotation/${opportunityId}`,
    },
    { apiName: this.apiName,...config });
  

  convertQuotationToSalesOrder = (quotationId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-quotation-to-sales-order/${quotationId}`,
    },
    { apiName: this.apiName,...config });
  

  convertSalesOrderToDeliveryNote = (salesOrderId: string, selectedItems?: PartialDeliveryItemDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-sales-order-to-delivery-note/${salesOrderId}`,
      body: selectedItems,
    },
    { apiName: this.apiName,...config });
  

  convertSalesOrderToDeliveryNoteByDate = (salesOrderId: string, untilDeliveryDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-sales-order-to-delivery-note-by-date/${salesOrderId}`,
      params: { untilDeliveryDate },
    },
    { apiName: this.apiName,...config });
  

  convertSalesOrderToMaterialRequest = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      responseType: 'text',
      url: `/api/app/document-conversion/convert-sales-order-to-material-request/${salesOrderId}`,
    },
    { apiName: this.apiName,...config });
  

  convertSalesOrderToSalesInvoice = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      url: `/api/app/document-conversion/convert-sales-order-to-sales-invoice/${salesOrderId}`,
    },
    { apiName: this.apiName,...config });
}