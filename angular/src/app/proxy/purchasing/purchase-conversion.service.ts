import type { PurchaseInvoiceDto, PurchaseOrderDto, PurchaseReceiptDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PurchaseConversionService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  convertMaterialRequestToPurchaseOrder = (materialRequestId: string, supplierId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseOrderDto>({
      method: 'POST',
      url: '/api/app/purchase-conversion/convert-material-request-to-purchase-order',
      params: { materialRequestId, supplierId },
    },
    { apiName: this.apiName,...config });
  

  convertPurchaseOrderToInvoice = (purchaseOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'POST',
      url: `/api/app/purchase-conversion/convert-purchase-order-to-invoice/${purchaseOrderId}`,
    },
    { apiName: this.apiName,...config });
  

  convertPurchaseOrderToReceipt = (purchaseOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseReceiptDto>({
      method: 'POST',
      url: `/api/app/purchase-conversion/convert-purchase-order-to-receipt/${purchaseOrderId}`,
    },
    { apiName: this.apiName,...config });
  

  convertPurchaseReceiptToInvoice = (purchaseReceiptId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PurchaseInvoiceDto>({
      method: 'POST',
      url: `/api/app/purchase-conversion/convert-purchase-receipt-to-invoice/${purchaseReceiptId}`,
    },
    { apiName: this.apiName,...config });
}