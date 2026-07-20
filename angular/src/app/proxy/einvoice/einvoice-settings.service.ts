import type { EInvoiceConnectResultDto, EInvoiceConnectionStatusDto, SaveEInvoiceCertificateDto, SaveEInvoiceCredentialsDto, TaxpayerSearchResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EInvoiceSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  connect = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceConnectResultDto>({
      method: 'POST',
      url: '/api/app/e-invoice-settings/connect',
    },
    { apiName: this.apiName,...config });
  

  getConnectionStatus = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceConnectionStatusDto>({
      method: 'GET',
      url: '/api/app/e-invoice-settings/connection-status',
    },
    { apiName: this.apiName,...config });
  

  saveCertificate = (input: SaveEInvoiceCertificateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/e-invoice-settings/save-certificate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  saveCredentials = (input: SaveEInvoiceCredentialsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/e-invoice-settings/save-credentials',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  searchTaxpayer = (idType: string, idValue: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxpayerSearchResultDto>({
      method: 'POST',
      url: '/api/app/e-invoice-settings/search-taxpayer',
      params: { idType, idValue },
    },
    { apiName: this.apiName,...config });
}