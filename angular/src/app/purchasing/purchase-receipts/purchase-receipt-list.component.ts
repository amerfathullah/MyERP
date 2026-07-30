import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseReceiptStore } from '../store/purchase-receipt.store';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-purchase-receipt-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    LocalizationPipe,
    PageModule,
    StatusBadgeComponent,
    PaginationComponent,
    SortableHeaderComponent],
  templateUrl: './purchase-receipt-list.component.html',
  styleUrls: ['./purchase-receipt-list.component.scss'],
})
export class PurchaseReceiptListComponent implements OnInit {
  readonly store = inject(PurchaseReceiptStore);
  private companyContext = inject(CompanyContextService);
  private conversionService = inject(PurchaseConversionService);
  private receiptService = inject(PurchaseReceiptService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'postingDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  fromDate = '';
  toDate = '';

  // Batch selection
  selectedIds = signal<Set<string>>(new Set());
  isCreatingInvoice = signal(false);
  isSubmitting = signal(false);
  hasSelection = computed(() => this.selectedIds().size > 0);
  selectionCount = computed(() => this.selectedIds().size);

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

  // Selection
  isSelected(id: string | undefined): boolean {
    return !!id && this.selectedIds().has(id);
  }

  toggleSelection(id: string | undefined, event: Event): void {
    if (!id) return;
    const updated = new Set(this.selectedIds());
    if ((event.target as HTMLInputElement).checked) {
      updated.add(id);
    } else {
      updated.delete(id);
    }
    this.selectedIds.set(updated);
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      const all = new Set(this.store.entities().filter(r => r.status === 'Submitted').map(r => r.id!));
      this.selectedIds.set(all);
    } else {
      this.selectedIds.set(new Set());
    }
  }

  clearSelection(): void {
    this.selectedIds.set(new Set());
  }

  bulkSubmitSelected(): void {
    const draftIds = Array.from(this.selectedIds())
      .filter(id => {
        const r: any = this.store.entities().find((e: any) => e.id === id);
        return r && (r.status === 'Draft' || r.status === 0);
      });
    if (draftIds.length === 0) {
      this.toaster.info(this.l.instant('::NoOrdersReadyForBilling'));
      return;
    }
    this.isSubmitting.set(true);
    this.receiptService.bulkSubmit(draftIds).subscribe({
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
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    this.isCreatingInvoice.set(true);
    // Create invoice from first selected receipt
    this.conversionService.convertPurchaseReceiptToInvoice(ids[0]).subscribe({
      next: (inv) => {
        this.isCreatingInvoice.set(false);
        this.clearSelection();
        this.toaster.success(this.l.instant('::InvoiceCreatedSuccessfully'));
        this.router.navigate(['/purchasing/invoices', inv.id]);
      },
      error: () => {
        this.isCreatingInvoice.set(false);
        this.toaster.error(this.l.instant('::ConversionFailed'));
      },
    });
  }

  exportCsv(): void {
    const rows = this.store.entities().map(r => ({
      receiptNumber: r.receiptNumber,
      postingDate: r.postingDate,
      supplierName: r.supplierName ?? '',
      grandTotal: r.grandTotal,
      status: r.status,
      perBilled: r.perBilled ?? 0,
    }));
    exportToCsv('purchase-receipts.csv', rows, ['receiptNumber', 'postingDate', 'supplierName', 'grandTotal', 'status', 'perBilled']);
  }
}
