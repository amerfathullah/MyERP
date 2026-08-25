import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { QualityInspectionTemplateService } from '../../proxy/inventory/quality-inspection-template.service';
import type { QiTemplateDto } from '../../proxy/inventory/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-quality-inspection-template-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'QualityInspectionTemplates' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-end">
          <a routerLink="/inventory/quality-inspection-templates/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ 'NewQITemplate' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          @if (isLoading) {
            <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (templates.length === 0) {
            <div class="text-center py-5 text-muted">{{ 'NoQITemplatesYet' | abpLocalization }}</div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Name' | abpLocalization }}</th>
                    <th>{{ 'Specification' | abpLocalization }}</th>
                    <th>{{ '::Status' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (t of templates; track t.id) {
                    <tr>
                      <td class="fw-semibold">{{ t.name }}</td>
                      <td>{{ t.parameterCount }} {{ 'AddParameter' | abpLocalization }}</td>
                      <td>
                        <span class="badge" [ngClass]="t.isEnabled ? 'bg-success' : 'bg-secondary'">
                          {{ (t.isEnabled ? '::Active' : '::Inactive') | abpLocalization }}
                        </span>
                      </td>
                      <td class="text-end">
                        <button class="btn btn-outline-secondary btn-sm me-1" (click)="toggle(t)">
                          <i class="fa" [ngClass]="t.isEnabled ? 'fa-toggle-off' : 'fa-toggle-on'"></i>
                        </button>
                        <button class="btn btn-outline-danger btn-sm" (click)="remove(t)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </td>
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
export class QualityInspectionTemplateListComponent implements OnInit {
  private service = inject(QualityInspectionTemplateService);
  private confirmation = inject(ConfirmationService);

  templates: QiTemplateDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.load();
  }

  load() {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize } as any).subscribe({
      next: (res) => { this.templates = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
  }

  toggle(t: QiTemplateDto) {
    this.service.toggle(t.id!).subscribe(() => this.load());
  }

  remove(t: QiTemplateDto) {
    this.confirmation.warn('::AreYouSureToDelete', t.name ?? '').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.delete(t.id!).subscribe(() => this.load());
      }
    });
  }

  onPageChange(e: PageEvent) {
    this.currentPage = e.pageIndex;
    this.pageSize = e.pageSize;
    this.load();
  }
}
