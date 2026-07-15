import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';

export interface LeaveAllocationDto {
  id: string;
  companyId: string;
  employeeId: string;
  leaveTypeId: string;
  fromDate: string;
  toDate: string;
  totalLeavesAllocated: number;
  carryForwardDays: number;
  leavesUsed: number;
  balance: number;
}

export interface CreateLeaveAllocationDto {
  companyId: string;
  employeeId: string;
  leaveTypeId: string;
  fromDate: string;
  toDate: string;
  totalLeavesAllocated: number;
  carryForwardDays?: number;
}

export interface BulkLeaveAllocationDto {
  companyId: string;
  leaveTypeId: string;
  fromDate: string;
  toDate: string;
  totalLeavesPerEmployee: number;
}

@Injectable({ providedIn: 'root' })
export class LeaveAllocationService {
  private rest = inject(RestService);

  getList(params?: any): Observable<{ totalCount: number; items: LeaveAllocationDto[] }> {
    return this.rest.request({ method: 'GET', url: '/api/app/leave-allocation', params });
  }

  get(id: string): Observable<LeaveAllocationDto> {
    return this.rest.request({ method: 'GET', url: `/api/app/leave-allocation/${id}` });
  }

  getBalance(employeeId: string, leaveTypeId: string, asOfDate: string): Observable<number> {
    return this.rest.request({
      method: 'GET',
      url: '/api/app/leave-allocation/balance',
      params: { employeeId, leaveTypeId, asOfDate },
    });
  }

  create(input: CreateLeaveAllocationDto): Observable<LeaveAllocationDto> {
    return this.rest.request({ method: 'POST', url: '/api/app/leave-allocation', body: input });
  }

  bulkAllocate(input: BulkLeaveAllocationDto): Observable<number> {
    return this.rest.request({ method: 'POST', url: '/api/app/leave-allocation/bulk-allocate', body: input });
  }

  delete(id: string): Observable<void> {
    return this.rest.request({ method: 'DELETE', url: `/api/app/leave-allocation/${id}` });
  }
}


