import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { JournalEntryTemplateService } from '../../proxy/accounting/journal-entry-template.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { JournalEntryTemplateDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-journal-entry-template-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'JournalEntryTemplates' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'JournalEntryTemplates' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/accounting/journal-entry-templates/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewJournalEntryTemplate' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-file-invoice fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoJournalEntryTemplatesYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/accounting/journal-entry-templates/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewJournalEntryTemplate' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'TemplateName' | abpLocalization }}</th>
                <th>{{ 'VoucherType' | abpLocalization }}</th>
                <th>{{ 'Lines' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (t of items(); track t.id) {
                  <tr>
                    <td class="fw-semibold">{{ t.templateName }}</td>
                    <td>{{ t.voucherType }}</td>
                    <td>{{ t.lines?.length ?? 0 }}</td>
                    <td>
                      @if (t.isActive) {
                        <span class="badge bg-success">{{ 'Active' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-secondary">{{ 'Disabled' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/accounting/journal-entry-templates', t.id, 'edit']">
                          <i class="fa fa-edit"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(t)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize"
        [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class JournalEntryTemplateListComponent implements OnInit {
  private service = inject(JournalEntryTemplateService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  items = signal<JournalEntryTemplateDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.getList({
      companyId: this.companyContext.currentCompanyId(),
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
    } as any).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  delete(t: JournalEntryTemplateDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(t.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
