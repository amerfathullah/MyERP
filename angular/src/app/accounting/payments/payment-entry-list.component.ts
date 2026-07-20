import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { PaymentEntryStore } from '../store/payment-entry.store';
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
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'postingDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  fromDate = '';
  toDate = '';

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
}
