import type { GetLedgerHealthMonitorSettingsInput, GetLedgerHealthRecordsInput, LedgerHealthCheckRunResultDto, LedgerHealthMonitorSettingsDto, LedgerHealthRecordDto, RunLedgerHealthCheckDto, UpdateLedgerHealthMonitorSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LedgerHealthMonitorService {
  private restService = inject(RestService);
  apiName = 'Default';


  getSettings = (input: GetLedgerHealthMonitorSettingsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LedgerHealthMonitorSettingsDto>({
      method: 'GET',
      url: '/api/app/ledger-health-monitor/settings',
      params: { companyId: input.companyId },
    },
    { apiName: this.apiName,...config });


  updateSettings = (input: UpdateLedgerHealthMonitorSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LedgerHealthMonitorSettingsDto>({
      method: 'PUT',
      url: '/api/app/ledger-health-monitor/settings',
      body: input,
    },
    { apiName: this.apiName,...config });


  runCheck = (input: RunLedgerHealthCheckDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LedgerHealthCheckRunResultDto>({
      method: 'POST',
      url: '/api/app/ledger-health-monitor/run-check',
      body: input,
    },
    { apiName: this.apiName,...config });


  getRecords = (input: GetLedgerHealthRecordsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LedgerHealthRecordDto>>({
      method: 'GET',
      url: '/api/app/ledger-health-monitor/records',
      params: { companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}
