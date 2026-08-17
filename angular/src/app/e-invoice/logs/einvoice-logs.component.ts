import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { LhdnSuccessLogDto } from '../../proxy/einvoice/models';

@Component({
  selector: 'app-einvoice-logs',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    PageModule,
    PaginationComponent
  ],
  templateUrl: './einvoice-logs.component.html',
  styleUrls: ['./einvoice-logs.component.scss'],
})
export class EinvoiceLogsComponent implements OnInit {
  private eInvoiceService = inject(EInvoiceService);
  private toaster = inject(ToasterService);
  companyContext = inject(CompanyContextService);

  logs = signal<LhdnSuccessLogDto[]>([]);
  totalCount = signal<number>(0);
  currentPage = signal<number>(0);
  pageSize = 15;
  isLoading = signal<boolean>(false);
  refreshingId = signal<string | null>(null);

  // Filters
  searchFilter = signal<string>('');
  sourceDocType = signal<string>('');
  fromDate = signal<string>('');
  toDate = signal<string>('');

  ngOnInit(): void {
    this.companyContext.load();
    this.loadLogs(0);
  }

  loadLogs(page: number = 0): void {
    this.currentPage.set(page);
    this.isLoading.set(true);

    const companyId = this.companyContext.currentCompanyId();

    this.eInvoiceService.getSuccessLogs({
      companyId: companyId ? companyId : undefined,
      sourceDocumentType: this.sourceDocType() || undefined,
      searchFilter: this.searchFilter() || undefined,
      fromDate: this.fromDate() ? this.fromDate() : undefined,
      toDate: this.toDate() ? this.toDate() : undefined,
      skipCount: page * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: 'submittedAt DESC'
    }).subscribe({
      next: (result) => {
        this.logs.set(result.items ?? []);
        this.totalCount.set(result.totalCount ?? 0);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toaster.error('::FailedToLoad');
      },
    });
  }

  onPageChange(event: any): void {
    this.loadLogs(event.pageIndex);
  }

  refreshStatus(log: LhdnSuccessLogDto): void {
    if (!log.submissionId) return;
    this.refreshingId.set(log.id);

    this.eInvoiceService.refreshStatus(log.submissionId).subscribe({
      next: (sub) => {
        this.refreshingId.set(null);
        this.toaster.success(`Status updated: ${sub.status}`);
        this.loadLogs(this.currentPage());
      },
      error: (err) => {
        this.refreshingId.set(null);
        this.toaster.error(err?.error?.message || '::RefreshFailed');
      }
    });
  }
}
