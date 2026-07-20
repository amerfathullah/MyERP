import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { NotificationLogService } from '../../proxy/core/notification-log.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-notification-log-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NotificationLogs' | abpLocalization">
      <div class="row mb-3 g-2">
        <div class="col-md-3">
          <select class="form-select form-select-sm" [(ngModel)]="channelFilter" (change)="loadData()">
            <option value="">All Channels</option>
            <option value="Email">Email</option>
            <option value="InApp">In-App</option>
            <option value="Push">Push</option>
          </select>
        </div>
        <div class="col-md-3">
          <select class="form-select form-select-sm" [(ngModel)]="statusFilter" (change)="loadData()">
            <option value="">All Status</option>
            <option value="Queued">Queued</option>
            <option value="Sent">Sent</option>
            <option value="Failed">Failed</option>
            <option value="PermanentlyFailed">Permanently Failed</option>
          </select>
        </div>
        @if (failedCount > 0) {
          <div class="col-md-6 text-end">
            <span class="badge bg-danger fs-6"><i class="fa fa-exclamation-triangle me-1"></i>{{ failedCount }} failed</span>
          </div>
        }
      </div>
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && logs.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-bell-slash fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">No notification logs found.</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover table-sm mb-0">
            <thead><tr>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th>Recipient</th>
              <th>Subject</th>
              <th>Channel</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th>Document</th>
            </tr></thead>
            <tbody>
              @for (log of logs; track log.id) {
                <tr>
                  <td class="text-nowrap">{{ log.createdAt | date:'dd/MM/yyyy HH:mm' }}</td>
                  <td class="text-truncate" style="max-width:200px">{{ log.recipient }}</td>
                  <td class="text-truncate" style="max-width:250px">{{ log.subject ?? '—' }}</td>
                  <td><span class="badge" [class]="getChannelClass(log.channel)">{{ log.channel }}</span></td>
                  <td>
                    <span class="badge" [class]="getStatusClass(log.status)">{{ log.status }}</span>
                    @if (log.retryCount > 0) { <small class="text-muted ms-1">({{ log.retryCount }}x)</small> }
                  </td>
                  <td>
                    @if (log.documentType) {
                      <small class="text-muted">{{ log.documentType }}</small>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `
})
export class NotificationLogListComponent implements OnInit {
  private notificationLogService = inject(NotificationLogService);
  logs: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 50;
  currentPage = 0;
  channelFilter = '';
  statusFilter = '';
  failedCount = 0;

  ngOnInit() {
    this.loadData();
    this.notificationLogService.getFailedCount().subscribe({
      next: c => this.failedCount = c,
      error: () => {}
    });
  }

  loadData() {
    this.isLoading = true;
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    if (this.channelFilter) params.channel = this.channelFilter;
    if (this.statusFilter) params.status = this.statusFilter;
    this.notificationLogService.getList(params).subscribe({
      next: res => { this.logs = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  getChannelClass(ch: string): string {
    switch (ch) { case 'Email': return 'bg-primary'; case 'InApp': return 'bg-info'; case 'Push': return 'bg-warning text-dark'; default: return 'bg-secondary'; }
  }

  getStatusClass(st: string): string {
    switch (st) { case 'Sent': return 'bg-success'; case 'Failed': return 'bg-danger'; case 'PermanentlyFailed': return 'bg-dark'; case 'Queued': return 'bg-warning text-dark'; default: return 'bg-secondary'; }
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
