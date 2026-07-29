import type { RegisterFilterDto } from '../sales/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface SupplierPaymentSummaryReportDto {
  items: SupplierPaymentLineDto[];
  totalInvoiced: number;
  totalPaid: number;
  totalOutstanding: number;
  totalOverdueAmount: number;
  supplierCount: number;
}

export interface SupplierPaymentLineDto {
  supplierId: string;
  supplierName: string;
  invoiceCount: number;
  totalInvoiced: number;
  totalPaid: number;
  totalOutstanding: number;
  overdueCount: number;
  overdueAmount: number;
  paymentTimeliness: number;
}

@Injectable({
  providedIn: 'root',
})
export class SupplierPaymentSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupplierPaymentSummaryReportDto>({
      method: 'GET',
      url: '/api/app/supplier-payment-summary/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName, ...config });
}
