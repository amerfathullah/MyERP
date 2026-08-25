import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import { QualityFeedbackDocumentType } from '../../proxy/inventory/quality-feedback-document-type.enum';
import type { QualityFeedbackDto } from '../../proxy/inventory/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-quality-feedback-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'QualityFeedbacks' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <a routerLink="/inventory/quality-feedback-templates" class="btn btn-outline-secondary btn-sm">
            <i class="fa fa-list-check me-1"></i>{{ 'QualityFeedbackTemplates' | abpLocalization }}
          </a>
          <a routerLink="/inventory/quality-feedback/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ 'NewQualityFeedback' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          @if (isLoading) {
            <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (feedbacks.length === 0) {
            <div class="text-center py-5 text-muted">{{ '::NoData' | abpLocalization }}</div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ 'DocumentType' | abpLocalization }}</th>
                    <th>{{ 'DocumentName' | abpLocalization }}</th>
                    <th>{{ 'AverageRating' | abpLocalization }}</th>
                    <th>{{ '::Remarks' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (f of feedbacks; track f.id) {
                    <tr>
                      <td>{{ docTypeLabel(f.documentType) }}</td>
                      <td class="fw-semibold">{{ f.documentName }}</td>
                      <td>{{ averageRating(f) }}</td>
                      <td class="text-muted">{{ f.remarks }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
        <div class="card-footer d-flex justify-content-between align-items-center">
          <span class="text-muted small">Total: {{ totalCount }}</span>
          <app-pagination [totalCount]="totalCount" [currentPage]="currentPage" [pageSize]="pageSize"
            (pageChange)="onPageChange($event)"></app-pagination>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityFeedbackListComponent implements OnInit {
  private service = inject(QualityManagementService);

  feedbacks: QualityFeedbackDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.load();
  }

  load() {
    this.isLoading = true;
    this.service.getFeedbackList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize } as any).subscribe({
      next: (res) => { this.feedbacks = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
  }

  docTypeLabel(t?: QualityFeedbackDocumentType): string {
    return t === QualityFeedbackDocumentType.Customer ? 'Customer' : 'User';
  }

  averageRating(f: QualityFeedbackDto): string {
    const ratings = (f.parameters ?? []).map((p) => p.rating ?? 0);
    if (ratings.length === 0) return '-';
    const avg = ratings.reduce((a, b) => a + b, 0) / ratings.length;
    return avg.toFixed(1);
  }

  onPageChange(e: PageEvent) {
    this.currentPage = e.pageIndex;
    this.pageSize = e.pageSize;
    this.load();
  }
}
