import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentEntryStore } from '../store/payment-entry.store';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-payment-entry-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe,
    StatusBadgeComponent, PaginationComponent, SortableHeaderComponent],
  templateUrl: './payment-entry-list.component.html',
  styleUrls: ['./payment-entry-list.component.scss'],
})
export class PaymentEntryListComponent implements OnInit {
  readonly store = inject(PaymentEntryStore);
  private paymentEntryService = inject(PaymentEntryService);
  private toaster = inject(ToasterService);
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'postingDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  fromDate = '';
  toDate = '';
  selectedIds = new Set<string>();
  isBulkProcessing = false;

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

  exportCsv(): void {
    const data = this.store.entities().map(pe => ({
      'Payment #': pe.paymentNumber,
      'Date': pe.postingDate,
      'Type': pe.paymentType,
      'Amount': pe.paidAmount,
      'Status': pe.status,
    }));
    exportToCsv('payment-entries.csv', data, ['Payment #', 'Date', 'Type', 'Amount', 'Status']);
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
      this.store.entities().forEach(pe => {
        if (pe.id) this.selectedIds.add(pe.id);
      });
    } else {
      this.selectedIds.clear();
    }
  }

  isAllSelected(): boolean {
    const entities = this.store.entities();
    return entities.length > 0 && entities.every(pe => pe.id && this.selectedIds.has(pe.id));
  }

  clearSelection(): void {
    this.selectedIds.clear();
  }

  bulkSubmit(): void {
    const draftIds = this.store.entities()
      .filter(pe => pe.id && this.selectedIds.has(pe.id) && pe.status === 'Draft')
      .map(pe => pe.id!);

    if (draftIds.length === 0) return;

    this.isBulkProcessing = true;
    this.paymentEntryService.bulkSubmit(draftIds).subscribe({
      next: (result: any) => {
        this.isBulkProcessing = false;
        this.selectedIds.clear();
        if (result?.failed > 0) {
          this.toaster.warn(`${result.succeeded} submitted, ${result.failed} failed`, 'Bulk Submit');
        } else {
          this.toaster.success(`${result?.succeeded ?? draftIds.length} payments submitted`, 'Bulk Submit');
        }
        this.loadData();
      },
      error: () => {
        this.isBulkProcessing = false;
        this.toaster.error('::BulkOperationFailed');
      },
    });
  }

  bulkPost(): void {
    const submittedIds = this.store.entities()
      .filter(pe => pe.id && this.selectedIds.has(pe.id) && pe.status === 'Submitted')
      .map(pe => pe.id!);

    if (submittedIds.length === 0) return;

    this.isBulkProcessing = true;
    this.paymentEntryService.bulkPost(submittedIds).subscribe({
      next: (result: any) => {
        this.isBulkProcessing = false;
        this.selectedIds.clear();
        if (result?.failed > 0) {
          this.toaster.warn(`${result.succeeded} posted, ${result.failed} failed`, 'Bulk Post');
        } else {
          this.toaster.success(`${result?.succeeded ?? submittedIds.length} payments posted`, 'Bulk Post');
        }
        this.loadData();
      },
      error: () => {
        this.isBulkProcessing = false;
        this.toaster.error('::BulkOperationFailed');
      },
    });
  }
}
