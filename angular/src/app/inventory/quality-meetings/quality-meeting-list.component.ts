import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { QualityMeetingDto } from '../../proxy/inventory/models';
import { QualityMeetingStatus } from '../../proxy/inventory/quality-meeting-status.enum';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-quality-meeting-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'QualityMeetings' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div class="d-flex gap-2">
            <select class="form-select form-select-sm" style="max-width: 160px" [(ngModel)]="statusFilter" (change)="onFilter()">
              <option value="">{{ '::All' | abpLocalization }}</option>
              <option [value]="QualityMeetingStatus.Open">Open</option>
              <option [value]="QualityMeetingStatus.Closed">Closed</option>
            </select>
          </div>
          <a routerLink="/inventory/quality-meetings/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ 'NewQualityMeeting' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table class="table table-hover table-striped mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ 'MeetingDate' | abpLocalization }}</th>
                  <th>{{ 'Chairperson' | abpLocalization }}</th>
                  <th>{{ 'Attendees' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (isLoading) {
                  <tr>
                    <td colspan="5" class="text-center py-4">
                      <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
                    </td>
                  </tr>
                } @else if (filteredItems().length === 0) {
                  <tr>
                    <td colspan="5" class="text-center py-4 text-muted">
                      {{ '::NoData' | abpLocalization }}
                    </td>
                  </tr>
                } @else {
                  @for (m of filteredItems(); track m.id) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="['/inventory/quality-meetings', m.id]">{{ m.meetingDate | date:'mediumDate' }}</a>
                      </td>
                      <td>{{ m.chairperson ?? '-' }}</td>
                      <td class="text-truncate" style="max-width: 250px">{{ m.attendees ?? '-' }}</td>
                      <td>
                        <span class="badge" [ngClass]="m.status === QualityMeetingStatus.Closed ? 'bg-secondary' : 'bg-success'">
                          {{ m.status === QualityMeetingStatus.Closed ? 'Closed' : 'Open' }}
                        </span>
                      </td>
                      <td class="text-end">
                        <a [routerLink]="['/inventory/quality-meetings', m.id]" class="btn btn-outline-secondary btn-sm">
                          <i class="fa fa-eye"></i>
                        </a>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </div>
        <div class="card-footer d-flex justify-content-between align-items-center">
          <span class="text-muted small">Total: {{ totalCount }}</span>
          <app-pagination
            [totalCount]="totalCount"
            [currentPage]="currentPage"
            [pageSize]="pageSize"
            (pageChange)="onPageChange($event)"
          ></app-pagination>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityMeetingListComponent implements OnInit {
  private readonly service = inject(QualityManagementService);
  readonly QualityMeetingStatus = QualityMeetingStatus;

  items: QualityMeetingDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 10;
  statusFilter = '';

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.isLoading = true;
    this.service.getMeetingList({
      maxResultCount: this.pageSize,
      skipCount: this.currentPage * this.pageSize,
    }).subscribe({
      next: (res) => {
        this.items = res.items ?? [];
        this.totalCount = res.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  onFilter() {
    this.currentPage = 0;
    this.loadData();
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  filteredItems() {
    if (this.statusFilter === '') return this.items;
    return this.items.filter(i => i.status === Number(this.statusFilter));
  }
}
