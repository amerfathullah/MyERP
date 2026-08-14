import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityReviewStore } from '../store/quality-review.store';
import { QualityReviewStatus } from '../../proxy/inventory/quality-review-status.enum';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-quality-review-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'QualityReviews' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div class="d-flex gap-2">
            <select class="form-select form-select-sm" style="max-width: 160px" [(ngModel)]="statusFilter" (change)="onFilter()">
              <option value="">{{ '::All' | abpLocalization }}</option>
              <option [value]="QualityReviewStatus.Open">Open</option>
              <option [value]="QualityReviewStatus.Passed">Passed</option>
              <option [value]="QualityReviewStatus.Failed">Failed</option>
            </select>
          </div>
          <a routerLink="/inventory/quality-reviews/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ 'NewQualityReview' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table class="table table-hover table-striped mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::ActualValue' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::Notes' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (store.isLoading()) {
                  <tr>
                    <td colspan="5" class="text-center py-4">
                      <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
                    </td>
                  </tr>
                } @else if (filteredReviews().length === 0) {
                  <tr>
                    <td colspan="5" class="text-center py-4 text-muted">
                      {{ '::NoData' | abpLocalization }}
                    </td>
                  </tr>
                } @else {
                  @for (review of filteredReviews(); track review.id) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="['/inventory/quality-reviews', review.id]">{{ review.reviewDate | date:'mediumDate' }}</a>
                      </td>
                      <td>{{ review.actualValue ?? '-' }}</td>
                      <td>
                        <span class="badge" [ngClass]="getStatusBadgeClass(review.status)">
                          {{ getStatusLabel(review.status) }}
                        </span>
                      </td>
                      <td class="text-truncate" style="max-width: 250px">{{ review.notes ?? '-' }}</td>
                      <td class="text-end">
                        <a [routerLink]="['/inventory/quality-reviews', review.id]" class="btn btn-outline-secondary btn-sm">
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
          <span class="text-muted small">Total: {{ store.totalCount() }}</span>
          <app-pagination
            [totalCount]="store.totalCount()"
            [currentPage]="currentPage"
            [pageSize]="pageSize"
            (pageChange)="onPageChange($event)"
          ></app-pagination>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityReviewListComponent implements OnInit {
  protected readonly store = inject(QualityReviewStore);
  readonly QualityReviewStatus = QualityReviewStatus;

  currentPage = 0;
  pageSize = 10;
  statusFilter = '';

  ngOnInit() {
    this.loadReviews();
  }

  loadReviews() {
    this.store.load({
      maxResultCount: this.pageSize,
      skipCount: this.currentPage * this.pageSize,
    });
  }

  onFilter() {
    this.currentPage = 0;
    this.loadReviews();
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadReviews();
  }

  filteredReviews() {
    const entities = this.store.entities();
    if (this.statusFilter === '') return entities;
    return entities.filter(r => r.status === Number(this.statusFilter));
  }

  getStatusBadgeClass(status?: QualityReviewStatus): string {
    switch (Number(status)) {
      case QualityReviewStatus.Passed:
        return 'bg-success';
      case QualityReviewStatus.Failed:
        return 'bg-danger';
      default:
        return 'bg-warning text-dark';
    }
  }

  getStatusLabel(status?: QualityReviewStatus): string {
    switch (Number(status)) {
      case QualityReviewStatus.Passed:
        return 'Passed';
      case QualityReviewStatus.Failed:
        return 'Failed';
      default:
        return 'Open';
    }
  }
}
