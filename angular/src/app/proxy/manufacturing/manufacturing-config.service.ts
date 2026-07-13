import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface WorkstationDto {
  id?: string;
  companyId?: string;
  name?: string;
  workstationType?: string;
  productionCapacity?: number;
  hourRate?: number;
  isActive?: boolean;
}

export interface OperationDto {
  id?: string;
  name?: string;
  workstationType?: string;
  isCorrectiveOperation?: boolean;
  isActive?: boolean;
}

export interface RoutingDto {
  id?: string;
  name?: string;
  isDisabled?: boolean;
  operations?: RoutingOperationDto[];
}

export interface RoutingOperationDto {
  id?: string;
  operationId?: string;
  sequenceId?: number;
  timeInMins?: number;
  workstationId?: string;
  operatingCost?: number;
}

@Injectable({ providedIn: 'root' })
export class WorkstationService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<WorkstationDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/workstations',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class OperationService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OperationDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/operations',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}

@Injectable({ providedIn: 'root' })
export class RoutingService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RoutingDto>>({
      method: 'GET',
      url: '/api/app/manufacturing/routings',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}
