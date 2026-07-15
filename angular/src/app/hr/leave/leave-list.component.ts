import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LeaveService } from '../../proxy/human-resources/leave.service';
import type { LeaveApplicationDto } from '../../proxy/human-resources/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-leave-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'LeaveApplications' | abpLocalization">
      <div class="d-flex justify-content-end mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/hr/leave/apply">
          <i class="fa fa-plus me-1"></i>{{ 'ApplyLeave' | abpLocalization }}
        </button>
      </div>
      @if (isLoading()) { <app-loading-overlay /> }
      @if (!isLoading() && items().length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-calendar-minus fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoLeaveApplicationsYet' | abpLocalization }}</p>
          <button class="btn btn-primary" routerLink="/hr/leave/apply">
            <i class="fa fa-plus me-1"></i>{{ 'ApplyLeave' | abpLocalization }}
          </button>
        </div>
      } @else if (!isLoading()) {
        <div class="table-responsive">
          <table class="table table-hover align-middle">
            <thead>
              <tr>
                <th>{{ 'Employee' | abpLocalization }}</th>
                <th>{{ 'LeaveType' | abpLocalization }}</th>
                <th>{{ 'FromDate' | abpLocalization }}</th>
                <th>{{ 'ToDate' | abpLocalization }}</th>
                <th class="text-end">{{ 'Days' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (leave of items(); track leave.id) {
                <tr>
                  <td>{{ leave.employeeName ?? leave.employeeId }}</td>
                  <td>{{ leave.leaveTypeName }}</td>
                  <td>{{ leave.fromDate | date:'dd/MM/yyyy' }}</td>
                  <td>{{ leave.toDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end">{{ leave.totalLeaveDays }}</td>
                  <td><app-status-badge [status]="getStatus(leave.status)" /></td>
                  <td>
                    @if (leave.status === 0) {
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-success" (click)="approve(leave.id!)">
                          <i class="fa fa-check"></i>
                        </button>
                        <button class="btn btn-outline-danger" (click)="reject(leave.id!)">
                          <i class="fa fa-times"></i>
                        </button>
                      </div>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
      <app-pagination [totalCount]="0" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
  </abp-page>
  `,
})
export class LeaveListComponent implements OnInit {
  private service = inject(LeaveService);
  items = signal<LeaveApplicationDto[]>([]);
  isLoading = signal(false);

  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.load();
  }

  load() {
    this.isLoading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 20 }).subscribe({
      next: r => { this.items.set(r.items ?? []); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  getStatus(s: number | undefined): string {
    return ['Open', 'Approved', 'Rejected', 'Cancelled'][s ?? 0] ?? 'Open';
  }

  approve(id: string) {
    this.service.approve(id).subscribe(() => this.load());
  }

  reject(id: string) {
    this.service.reject(id).subscribe(() => this.load());
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; /* reload handled by store */; }
}