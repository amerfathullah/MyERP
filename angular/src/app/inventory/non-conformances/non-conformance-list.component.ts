import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { NonConformanceDto } from '../../proxy/inventory/models';
import { NonConformanceStatus } from '../../proxy/inventory/non-conformance-status.enum';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-non-conformance-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'NonConformances' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div class="d-flex gap-2">
            <select class="form-select form-select-sm" style="max-width: 160px" [(ngModel)]="statusFilter" (change)="onFilter()">
              <option value="">{{ '::All' | abpLocalization }}</option>
              <option [value]="NonConformanceStatus.Open">Open</option>
              <option [value]="NonConformanceStatus.Resolved">Resolved</option>
              <option [value]="NonConformanceStatus.Cancelled">Cancelled</option>
            </select>
          </div>
          <a routerLink="/inventory/non-conformances/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ 'NewNonConformance' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table class="table table-hover table-striped mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Subject' | abpLocalization }}</th>
                  <th>{{ '::ProcessOwner' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::ResolutionDate' | abpLocalization }}</th>
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
                  @for (nc of filteredItems(); track nc.id) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="['/inventory/non-conformances', nc.id]">{{ nc.subject }}</a>
                      </td>
                      <td>{{ nc.processOwner ?? '-' }}</td>
                      <td>
                        <span class="badge" [ngClass]="getStatusBadgeClass(nc.status)">
                          {{ getStatusLabel(nc.status) }}
                        </span>
                      </td>
                      <td>{{ nc.resolutionDate ? (nc.resolutionDate | date:'mediumDate') : '-' }}</td>
                      <td class="text-end">
                        <a [routerLink]="['/inventory/non-conformances', nc.id]" class="btn btn-outline-secondary btn-sm">
                          <i class="fa fa-pencil"></i>
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
export class NonConformanceListComponent implements OnInit {
  private readonly service = inject(QualityManagementService);
  readonly NonConformanceStatus = NonConformanceStatus;

  items: NonConformanceDto[] = [];
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
    this.service.getNonConformanceList({
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

  getStatusBadgeClass(status?: NonConformanceStatus): string {
    switch (Number(status)) {
      case NonConformanceStatus.Resolved:
        return 'bg-success';
      case NonConformanceStatus.Cancelled:
        return 'bg-secondary';
      default:
        return 'bg-danger';
    }
  }

  getStatusLabel(status?: NonConformanceStatus): string {
    switch (Number(status)) {
      case NonConformanceStatus.Resolved:
        return 'Resolved';
      case NonConformanceStatus.Cancelled:
        return 'Cancelled';
      default:
        return 'Open';
    }
  }
}
