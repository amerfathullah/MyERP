import type {
  AvailableStockForPackingItemDto,
  AvailableStockForPackingItemsInputDto,
  CustomerAcquisitionAndLoyaltyInputDto,
  CustomerAcquisitionAndLoyaltyReportDto,
  CustomerCreditBalanceDto,
  CustomerCreditBalanceInputDto,
  CustomerWiseItemPriceDto,
  CustomerWiseItemPriceInputDto,
  CustomerWithoutSalesDto,
  CustomersWithoutSalesInputDto,
  SalesAnalyticsReportDto,
  SalesAnalyticsRequestDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesAnalyticsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SalesAnalyticsRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesAnalyticsReportDto>({
      method: 'GET',
      url: '/api/app/sales-analytics/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, groupBy: input.groupBy, periodType: input.periodType, valueField: input.valueField, entityIds: input.entityIds },
    },
    { apiName: this.apiName, ...config });

  getCustomerCreditBalanceReport = (input: CustomerCreditBalanceInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerCreditBalanceDto[]>({
      method: 'GET',
      url: '/api/app/sales-analytics/customer-credit-balance-report',
      params: { companyId: input.companyId, customerId: input.customerId },
    },
    { apiName: this.apiName, ...config });

  getCustomersWithoutSalesTransactions = (input: CustomersWithoutSalesInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerWithoutSalesDto[]>({
      method: 'GET',
      url: '/api/app/sales-analytics/customers-without-sales-transactions',
      params: { companyId: input.companyId },
    },
    { apiName: this.apiName, ...config });

  getCustomerWiseItemPriceReport = (input: CustomerWiseItemPriceInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerWiseItemPriceDto[]>({
      method: 'GET',
      url: '/api/app/sales-analytics/customer-wise-item-price-report',
      params: { customerId: input.customerId, priceListId: input.priceListId, itemId: input.itemId, companyId: input.companyId },
    },
    { apiName: this.apiName, ...config });

  getCustomerAcquisitionAndLoyaltyReport = (input: CustomerAcquisitionAndLoyaltyInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerAcquisitionAndLoyaltyReportDto>({
      method: 'GET',
      url: '/api/app/sales-analytics/customer-acquisition-and-loyalty-report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, customerId: input.customerId, territoryId: input.territoryId, treeView: input.treeView },
    },
    { apiName: this.apiName, ...config });

  getAvailableStockForPackingItemsReport = (input: AvailableStockForPackingItemsInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AvailableStockForPackingItemDto[]>({
      method: 'GET',
      url: '/api/app/sales-analytics/available-stock-for-packing-items-report',
      params: { companyId: input.companyId, warehouseId: input.warehouseId, bundleItemId: input.bundleItemId },
    },
    { apiName: this.apiName, ...config });
}