import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DeliveryNoteStore } from '../store/delivery-note.store';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';

@Component({
  selector: 'app-delivery-note-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    PaginationComponent,
    SortableHeaderComponent],
  templateUrl: './delivery-note-list.component.html',
  styleUrls: ['./delivery-note-list.component.scss'],
})
export class DeliveryNoteListComponent implements OnInit {
  readonly store = inject(DeliveryNoteStore);
  private companyContext = inject(CompanyContextService);
  private salesInvoiceService = inject(SalesInvoiceService);
  private dnService = inject(DeliveryNoteService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  displayedColumns = ['deliveryNumber', 'postingDate', 'grandTotal', 'status', 'actions'];
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'postingDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  fromDate = '';
  toDate = '';

  // Batch invoicing selection
  selectedDnIds = signal<Set<string>>(new Set());
  isCreatingInvoice = signal(false);
  isSubmitting = signal(false);

  hasSelection = computed(() => this.selectedDnIds().size > 0);
  selectionCount = computed(() => this.selectedDnIds().size);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.store.load({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : 'postingDate DESC',
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      companyId: this.companyContext.currentCompanyId() || undefined,
    });
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.currentPage = 0;
    this.loadData();
  }

  onStatusChange(status: string): void {
    this.statusFilter = status;
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

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  exportCsv(): void {
    const data = this.store.entities().map(d => ({
      'Delivery #': d.deliveryNumber,
      'Date': d.postingDate,
      'Total': d.grandTotal,
      'Status': d.status,
    }));
    exportToCsv('delivery-notes.csv', data, ['Delivery #', 'Date', 'Total', 'Status']);
  }

  // --- Batch Invoicing ---

  toggleSelection(dnId: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const newSet = new Set(this.selectedDnIds());
    if (checked) {
      newSet.add(dnId);
    } else {
      newSet.delete(dnId);
    }
    this.selectedDnIds.set(newSet);
  }

  isSelected(dnId: string): boolean {
    return this.selectedDnIds().has(dnId);
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      // Select all submitted/posted DNs on current page
      const eligible = this.store.entities()
        .filter((dn: any) => dn.status === 'Submitted' || dn.status === 'Posted' || dn.status === 3 || dn.status === 5)
        .map((dn: any) => dn.id);
      this.selectedDnIds.set(new Set(eligible));
    } else {
      this.selectedDnIds.set(new Set());
    }
  }

  clearSelection(): void {
    this.selectedDnIds.set(new Set());
  }

  bulkSubmitSelected(): void {
    const draftIds = Array.from(this.selectedDnIds())
      .filter(id => {
        const dn: any = this.store.entities().find((e: any) => e.id === id);
        return dn && (dn.status === 'Draft' || dn.status === 0);
      });
    if (draftIds.length === 0) {
      this.toaster.info(this.l.instant('::NoOrdersReadyForBilling'));
      return;
    }
    this.isSubmitting.set(true);
    this.dnService.bulkSubmit(draftIds).subscribe({
      next: (result: any) => {
        this.isSubmitting.set(false);
        this.clearSelection();
        if (result.succeeded > 0) this.toaster.success(`${result.succeeded} ${this.l.instant('::SuccessfullySubmitted')}`);
        if (result.failed > 0) this.toaster.warn(`${result.failed} ${this.l.instant('::OperationFailed')}`);
        this.loadData();
      },
      error: () => { this.isSubmitting.set(false); this.toaster.error(this.l.instant('::BulkOperationFailed')); },
    });
  }

  createInvoiceFromSelected(): void {
    const ids = Array.from(this.selectedDnIds());
    if (ids.length === 0) return;

    // Resolve customer from first selected DN
    const firstDn: any = this.store.entities().find((dn: any) => dn.id === ids[0]);
    if (!firstDn) return;

    this.isCreatingInvoice.set(true);
    this.salesInvoiceService.createFromDeliveryNotes({
      companyId: this.companyContext.currentCompanyId(),
      customerId: firstDn.customerId,
      deliveryNoteIds: ids,
      currencyCode: firstDn.currencyCode || 'MYR',
    } as any).subscribe({
      next: (result) => {
        this.isCreatingInvoice.set(false);
        this.toaster.success(this.l.instant('::SuccessfullyCreated'));
        this.clearSelection();
        this.router.navigate(['/sales/invoices', result.id]);
      },
      error: (err) => {
        this.isCreatingInvoice.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      },
    });
  }
}
