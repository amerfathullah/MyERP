import type { CreateShiftAssignmentDto, GetShiftAssignmentListDto, ShiftAssignmentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ShiftAssignmentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateShiftAssignmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShiftAssignmentDto>({
      method: 'POST',
      url: '/api/app/shift-assignment',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/shift-assignment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShiftAssignmentDto>({
      method: 'GET',
      url: `/api/app/shift-assignment/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetShiftAssignmentListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ShiftAssignmentDto>>({
      method: 'GET',
      url: '/api/app/shift-assignment',
      params: { companyId: input.companyId, employeeId: input.employeeId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateShiftAssignmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ShiftAssignmentDto>({
      method: 'PUT',
      url: `/api/app/shift-assignment/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}