import type { DeliveryNoteDto, PartialDeliveryItemDto, QuotationDto, SalesInvoiceDto, SalesOrderDto, SalesOrderPurchaseItemSelectionDto } from './models';
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
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-delivery-note-to-sales-invoice/${deliveryNoteId}`,
    },
      { apiName: this.apiName,...config });
  

  
  convertOpportunityToQuotation = (opportunityId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-opportunity-to-quotation/${opportunityId}`,
    },
      { apiName: this.apiName,...config });
  

  
  convertQuotationToSalesInvoice = (quotationId: string, selectedItemIds?: readonly string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-quotation-to-sales-invoice/${quotationId}`,
      body: selectedItemIds,
    },
      { apiName: this.apiName,...config });
  

  
  convertQuotationToSalesOrder = (quotationId: string, selectedItemIds?: readonly string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesOrderDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-quotation-to-sales-order/${quotationId}`,
      body: selectedItemIds,
    },
      { apiName: this.apiName,...config });
  

  
  convertSalesOrderToDeliveryNote = (salesOrderId: string, selectedItems?: readonly PartialDeliveryItemDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-sales-order-to-delivery-note/${salesOrderId}`,
      body: selectedItems,
    },
      { apiName: this.apiName,...config });
  

  
  convertSalesOrderToDeliveryNoteByDate = (salesOrderId: string, untilDeliveryDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-sales-order-to-delivery-note-by-date/${salesOrderId}`,
      params: { untilDeliveryDate },
    },
      { apiName: this.apiName,...config });
  

  
  convertSalesOrderToMaterialRequest = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-sales-order-to-material-request/${salesOrderId}`,
    },
      { apiName: this.apiName,...config });
  

  
  convertSalesOrderToPickList = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-sales-order-to-pick-list/${salesOrderId}`,
    },
      { apiName: this.apiName,...config });
  

  
  convertSalesOrderToPurchaseOrders = (salesOrderId: string, selectedItems: readonly SalesOrderPurchaseItemSelectionDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-sales-order-to-purchase-orders/${salesOrderId}`,
      body: selectedItems,
    },
      { apiName: this.apiName,...config });
  

  
  convertSalesOrderToSalesInvoice = (salesOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/convert-sales-order-to-sales-invoice/${salesOrderId}`,
    },
      { apiName: this.apiName,...config });
  

  
  makeDeliveryNoteReturn = (deliveryNoteId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryNoteDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/make-delivery-note-return/${deliveryNoteId}`,
    },
      { apiName: this.apiName,...config });
  

  
  makeSalesInvoiceReturn = (salesInvoiceId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesInvoiceDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/document-conversion/make-sales-invoice-return/${salesInvoiceId}`,
    },
      { apiName: this.apiName,...config });
}
