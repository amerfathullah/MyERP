import type { BarcodeScanResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BarcodeScanService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  scan = (barcode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BarcodeScanResultDto>({
      method: 'POST',
      url: '/api/app/barcode-scan/scan',
      params: { barcode },
    },
    { apiName: this.apiName,...config });
}