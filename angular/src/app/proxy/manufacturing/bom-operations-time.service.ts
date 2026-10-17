import type { BomOperationsTimeReportDto, BomOperationsTimeRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BomOperationsTimeService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: BomOperationsTimeRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomOperationsTimeReportDto>({
      method: 'POST',
      url: '/api/app/bom-operations-time/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
