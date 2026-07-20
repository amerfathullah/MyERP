import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
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
  private invoiceService = inject(SalesInvoiceService);
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

  ngOnInit(): void {
    this.loadData();
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
        this.toaster.error('Bulk operation failed', 'Error');
      },
    });
  }
}
