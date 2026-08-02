import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { Confirmation } from '@abp/ng.theme.shared';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { DatePresetsComponent, type DateRange } from '../../shared/components/date-presets/date-presets.component';
import { SalesInvoiceStore } from '../store/sales-invoice.store';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import type { SalesInvoiceListSummaryDto } from '../../proxy/sales/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-sales-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent,
    PaginationComponent,
    SortableHeaderComponent,
    DatePresetsComponent],
  templateUrl: './sales-invoice-list.component.html',
  styleUrls: ['./sales-invoice-list.component.scss'],
})
export class SalesInvoiceListComponent implements OnInit {
  readonly store = inject(SalesInvoiceStore);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);
  private l = inject(LocalizationService);
  private invoiceService = inject(SalesInvoiceService);
  private eInvoiceService = inject(EInvoiceService);
  private toaster = inject(ToasterService);
  displayedColumns = ['select', 'invoiceNumber', 'issueDate', 'customerName', 'grandTotal', 'status', 'eInvoiceStatus', 'actions'];
  selectedIds = new Set<string>();
  isBulkProcessing = false;
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'issueDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  fromDate = '';
  toDate = '';
  private searchTimeout: any;

  /** KPI summary for the list page header */
  summary = signal<SalesInvoiceListSummaryDto | null>(null);
  summaryLoading = signal(false);

  ngOnInit(): void {
    this.loadData();
    this.loadSummary();
  }

  /** Loads aggregate KPIs (outstanding, overdue, monthly revenue) via single API call. */
  loadSummary(): void {
    this.summaryLoading.set(true);
    const companyId = this.companyContext.currentCompanyId() || undefined;
    this.invoiceService.getListSummary(companyId).subscribe({
      next: (result) => {
        this.summary.set(result);
        this.summaryLoading.set(false);
      },
      error: () => this.summaryLoading.set(false),
    });
  }

  loadData(): void {
    this.store.loadInvoices({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : undefined,
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      companyId: this.companyContext.currentCompanyId() || undefined,
    });
  }

  createInvoice(): void {
    this.router.navigate(['/sales/invoices/new']);
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    clearTimeout(this.searchTimeout);
    this.searchTimeout = setTimeout(() => {
      this.currentPage = 0;
      this.loadData();
    }, 400);
  }

  onStatusFilter(status: string): void {
    this.statusFilter = status;
    this.currentPage = 0;
    this.loadData();
  }

  onDateRangeChange(from: string, to: string): void {
    this.fromDate = from;
    this.toDate = to;
    this.currentPage = 0;
    this.loadData();
  }

  onDatePreset(range: DateRange): void {
    this.fromDate = range.from;
    this.toDate = range.to;
    this.currentPage = 0;
    this.loadData();
  }

  onSort(event: SortEvent): void {
    this.sortField = event.field;
    this.sortDirection = event.direction;
    this.currentPage = 0;
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  exportCsv(): void {
    const data = this.store.entities().map(inv => ({
      'Invoice #': inv.invoiceNumber,
      'Date': inv.issueDate,
      'Customer': inv.customerName,
      'Total': inv.grandTotal,
      'Status': inv.status,
    }));
    exportToCsv('sales-invoices.csv', data, ['Invoice #', 'Date', 'Customer', 'Total', 'Status']);
  }

  submit(id: string): void {
    this.store.submitInvoice(id);
  }

  post(id: string): void {
    this.store.postInvoice(id);
  }

  cancel(id: string): void {
    this.confirmation.warn('::CancelConfirmationMessage', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.cancelInvoice(id);
      }
    });
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.deleteInvoice(id);
      }
    });
  }

  // ── Bulk Selection ──

  toggleSelect(id: string, checked: boolean): void {
    if (checked) {
      this.selectedIds.add(id);
    } else {
      this.selectedIds.delete(id);
    }
  }

  toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.store.entities().forEach(inv => {
        if (inv.id) this.selectedIds.add(inv.id);
      });
    } else {
      this.selectedIds.clear();
    }
  }

  isAllSelected(): boolean {
    const entities = this.store.entities();
    return entities.length > 0 && entities.every(inv => inv.id && this.selectedIds.has(inv.id));
  }

  clearSelection(): void {
    this.selectedIds.clear();
  }

  bulkSubmit(): void {
    const draftIds = this.store.entities()
      .filter(inv => inv.id && this.selectedIds.has(inv.id) && inv.status === 'Draft')
      .map(inv => inv.id!);

    if (draftIds.length === 0) return;

    this.isBulkProcessing = true;
    this.invoiceService.bulkSubmit(draftIds).subscribe({
      next: (result: any) => {
        this.isBulkProcessing = false;
        this.selectedIds.clear();
        if (result?.failed > 0) {
          this.toaster.warn(`${result.succeeded} submitted, ${result.failed} failed`, 'Bulk Submit');
        } else {
          this.toaster.success(`${result?.succeeded ?? draftIds.length} invoices submitted`, 'Bulk Submit');
        }
        this.loadData();
      },
      error: () => {
        this.isBulkProcessing = false;
        this.toaster.error('::BulkOperationFailed');
      },
    });
  }

  bulkCreatePayment(): void {
    const postedWithOutstanding = this.store.entities()
      .filter(inv => inv.id && this.selectedIds.has(inv.id) && inv.status === 'Posted' && this.getOutstanding(inv) > 0);
    if (postedWithOutstanding.length === 0) {
      this.toaster.info(this.l.instant('::NoInvoicesSelected'));
      return;
    }
    const totalAmount = postedWithOutstanding.reduce((sum, inv) => sum + this.getOutstanding(inv), 0);
    const invoiceIds = postedWithOutstanding.map(inv => inv.id!).join(',');
    this.router.navigate(['/accounting/payments/new'], {
      queryParams: {
        partyType: 'Customer',
        amount: totalAmount.toFixed(2),
        invoiceIds,
        companyId: this.companyContext.currentCompanyId(),
      },
    });
  }

  batchSubmitToLhdn(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) { this.toaster.warn(this.l.instant('::PleaseSelectCompanyFirst')); return; }

    const postedIds = this.store.entities()
      .filter(inv => inv.id && this.selectedIds.has(inv.id) && inv.status === 'Posted' && (!inv.eInvoiceStatus || inv.eInvoiceStatus === 'NotSubmitted'))
      .map(inv => inv.id!);

    if (postedIds.length === 0) {
      this.toaster.info(this.l.instant('::NoneEligibleForLhdn'));
      return;
    }

    this.isBulkProcessing = true;
    this.eInvoiceService.batchSubmit({
      companyId,
      sourceDocumentType: 'SalesInvoice',
      documentIds: postedIds,
    }).subscribe({
      next: (result) => {
        this.isBulkProcessing = false;
        this.selectedIds.clear();
        const msg = `${result.succeededCount ?? 0} submitted, ${result.failedCount ?? 0} failed, ${result.skippedCount ?? 0} skipped`;
        if (result.failedCount && result.failedCount > 0) {
          this.toaster.warn(msg, this.l.instant('::SubmitToLHDN'));
        } else {
          this.toaster.success(msg, this.l.instant('::SubmitToLHDN'));
        }
        this.loadData();
      },
      error: () => {
        this.isBulkProcessing = false;
        this.toaster.error(this.l.instant('::LhdnBatchSubmitFailed'));
      },
    });
  }

  // ── Outstanding & Overdue Helpers ──

  getOutstanding(inv: any): number {
    const outstanding = (inv.grandTotal ?? 0) - (inv.amountPaid ?? 0) - (inv.writeOffAmount ?? 0) - (inv.totalAdvance ?? 0);
    return Math.max(0, outstanding);
  }

  isInvoiceOverdue(inv: any): boolean {
    if (inv.status !== 'Posted' || inv.isReturn) return false;
    if (!inv.dueDate) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const due = new Date(inv.dueDate);
    due.setHours(0, 0, 0, 0);
    return due < today && this.getOutstanding(inv) > 0.01;
  }
}
