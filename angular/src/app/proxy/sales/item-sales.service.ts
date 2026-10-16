import type { GetItemWiseSalesHistoryDto, ItemSalesReportDto, ItemWiseSalesHistoryReportDto, RegisterFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ItemSalesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: RegisterFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemSalesReportDto>({
      method: 'GET',
      url: '/api/app/item-sales/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });

  getHistoryReport = (input: GetItemWiseSalesHistoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ItemWiseSalesHistoryReportDto>({
      method: 'GET',
      url: '/api/app/item-sales/history-report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        itemId: input.itemId,
        itemCode: input.itemCode,
        itemGroupId: input.itemGroupId,
        customerId: input.customerId,
      },
    },
    { apiName: this.apiName, ...config });
}