import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { QualityFeedbackTemplateDto } from '../../proxy/inventory/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-quality-feedback-template-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'QualityFeedbackTemplates' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <a routerLink="/inventory/quality-feedback" class="btn btn-outline-secondary btn-sm">
            <i class="fa fa-arrow-left me-1"></i>{{ 'QualityFeedbacks' | abpLocalization }}
          </a>
          <a routerLink="/inventory/quality-feedback-templates/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          @if (isLoading) {
            <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (templates.length === 0) {
            <div class="text-center py-5 text-muted">{{ '::NoData' | abpLocalization }}</div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Name' | abpLocalization }}</th>
                    <th>Parameters</th>
                  </tr>
                </thead>
                <tbody>
                  @for (t of templates; track t.id) {
                    <tr>
                      <td class="fw-semibold">{{ t.templateName }}</td>
                      <td>{{ (t.parameters ?? []).length }}</td>
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
export class QualityFeedbackTemplateListComponent implements OnInit {
  private service = inject(QualityManagementService);

  templates: QualityFeedbackTemplateDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.load();
  }

  load() {
    this.isLoading = true;
    this.service.getFeedbackTemplateList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize } as any).subscribe({
      next: (res) => { this.templates = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
  }

  onPageChange(e: PageEvent) {
    this.currentPage = e.pageIndex;
    this.pageSize = e.pageSize;
    this.load();
  }
}
