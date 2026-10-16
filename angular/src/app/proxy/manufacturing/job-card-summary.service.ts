import type { JobCardSummaryDto, JobCardSummaryRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class JobCardSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getSummary = (input: JobCardSummaryRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardSummaryDto>({
      method: 'POST',
      url: '/api/app/job-card-summary/summary',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
