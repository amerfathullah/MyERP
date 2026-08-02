import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { PurchaseInvoiceStore } from '../store/purchase-invoice.store';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-purchase-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent,
    PaginationComponent,
    SortableHeaderComponent],
  templateUrl: './purchase-invoice-list.component.html',
  styleUrls: ['./purchase-invoice-list.component.scss'],
})
export class PurchaseInvoiceListComponent implements OnInit {
  readonly store = inject(PurchaseInvoiceStore);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private invoiceService = inject(PurchaseInvoiceService);
  private eInvoiceService = inject(EInvoiceService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  displayedColumns = ['orderNumber', 'orderDate', 'grandTotal', 'status', 'actions'];
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'issueDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  fromDate = '';
  toDate = '';

  /** Server-side KPI summary (accurate across ALL invoices, not just current page) */
  summary = signal<any>(null);

  ngOnInit(): void {
    this.loadData();
    this.loadSummary();
  }

  loadSummary(): void {
    const companyId = this.companyContext.currentCompanyId() || undefined;
    this.invoiceService.getListSummary(companyId).subscribe({
      next: (result) => this.summary.set(result),
      error: () => {},
    });
  }

  loadData(): void {
    this.store.load({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : '',
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      companyId: this.companyContext.currentCompanyId() || undefined,
    });
  }

  onSearch(): void {
    this.currentPage = 0;
    this.loadData();
  }

  onStatusChange(): void {
    this.currentPage = 0;
    this.loadData();
  }

  onDateChange(): void {
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

  createInvoice(): void {
    this.router.navigate(['/purchasing/invoices/new']);
  }

  viewDetail(id: string): void {
    this.router.navigate(['/purchasing/invoices', id]);
  }

  submit(id: string): void {
    this.store.submitInvoice(id);
  }

  post(id: string): void {
    this.store.postInvoice(id);
  }

  cancel(id: string): void {
    this.store.cancelInvoice(id);
  }

  exportCsv(): void {
    const data = this.store.entities().map(inv => ({
      'Invoice #': inv.invoiceNumber,
      'Date': inv.issueDate,
      'Total': inv.grandTotal,
      'Status': inv.status,
    }));
    exportToCsv('purchase-invoices.csv', data, ['Invoice #', 'Date', 'Total', 'Status']);
  }

  isSubmittingLhdn = false;

  batchSubmitToLhdn(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) { this.toaster.warn(this.l.instant('::PleaseSelectCompanyFirst')); return; }

    const eligibleIds = this.store.entities()
      .filter(inv => inv.id && inv.status === 'Posted' && (!inv.eInvoiceStatus || inv.eInvoiceStatus === 'NotSubmitted'))
      .map(inv => inv.id!);

    if (eligibleIds.length === 0) {
      this.toaster.info(this.l.instant('::NoneEligibleForLhdn'));
      return;
    }

    this.isSubmittingLhdn = true;
    this.eInvoiceService.batchSubmit({
      companyId,
      sourceDocumentType: 'PurchaseInvoice',
      documentIds: eligibleIds,
    }).subscribe({
      next: (result) => {
        this.isSubmittingLhdn = false;
        const msg = `${result.succeededCount ?? 0} submitted, ${result.failedCount ?? 0} failed, ${result.skippedCount ?? 0} skipped`;
        if (result.failedCount && result.failedCount > 0) {
          this.toaster.warn(msg, this.l.instant('::SubmitToLHDN'));
        } else {
          this.toaster.success(msg, this.l.instant('::SubmitToLHDN'));
        }
        this.loadData();
      },
      error: () => {
        this.isSubmittingLhdn = false;
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

  isDueThisWeek(inv: any): boolean {
    if (inv.status !== 'Posted' || inv.isReturn) return false;
    if (!inv.dueDate) return false;
    if (this.getOutstanding(inv) <= 0.01) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const sevenDays = new Date(today);
    sevenDays.setDate(sevenDays.getDate() + 7);
    const due = new Date(inv.dueDate);
    due.setHours(0, 0, 0, 0);
    return due >= today && due <= sevenDays;
  }

  dueThisWeekCount(): number {
    return (this.store.entities() ?? []).filter((inv: any) => this.isDueThisWeek(inv)).length;
  }

  dueThisWeekTotal(): number {
    return (this.store.entities() ?? [])
      .filter((inv: any) => this.isDueThisWeek(inv))
      .reduce((sum: number, inv: any) => sum + this.getOutstanding(inv), 0);
  }

  overdueCount(): number {
    return (this.store.entities() ?? []).filter((inv: any) => this.isInvoiceOverdue(inv)).length;
  }

  overdueTotal(): number {
    return (this.store.entities() ?? [])
      .filter((inv: any) => this.isInvoiceOverdue(inv))
      .reduce((sum: number, inv: any) => sum + this.getOutstanding(inv), 0);
  }
}
