import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface HolidayListDto {
  id?: string;
  companyId?: string;
  name?: string;
  year?: number;
  weeklyOff?: string;
  isDefault?: boolean;
  holidays?: HolidayDto[];
  creationTime?: string;
}

export interface HolidayDto {
  id?: string;
  holidayDate?: string;
  description?: string;
  isWeeklyOff?: boolean;
}

export interface SalaryStructureDto {
  id?: string;
  companyId?: string;
  name?: string;
  isHourlyBased?: boolean;
  payrollFrequency?: string;
  isActive?: boolean;
  description?: string;
  details?: SalaryStructureDetailDto[];
}

export interface SalaryStructureDetailDto {
  id?: string;
  salaryComponentId?: string;
  componentName?: string;
  amount?: number;
  formula?: string;
}

@Injectable({ providedIn: 'root' })
export class HolidayListService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<HolidayListDto>>({
      method: 'GET',
      url: '/api/app/holiday-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, HolidayListDto>({
      method: 'GET',
      url: `/api/app/holiday-list/${id}`,
    }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class SalaryStructureService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalaryStructureDto>>({
      method: 'GET',
      url: '/api/app/salary-structure',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalaryStructureDto>({
      method: 'GET',
      url: `/api/app/salary-structure/${id}`,
    }, { apiName: this.apiName, ...config });
}
