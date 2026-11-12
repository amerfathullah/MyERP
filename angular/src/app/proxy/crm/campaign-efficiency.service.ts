import type { CampaignEfficiencyFilterDto, CampaignEfficiencyReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CampaignEfficiencyService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (filter: CampaignEfficiencyFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignEfficiencyReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/campaign-efficiency/report',
      params: { fromDate: filter.fromDate, toDate: filter.toDate, companyId: filter.companyId, basedOn: filter.basedOn },
    },
      { apiName: this.apiName,...config });
}
