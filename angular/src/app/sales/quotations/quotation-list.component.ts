import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QuotationStore } from '../store/quotation.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { QuotationDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-quotation-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, PaginationComponent, SortableHeaderComponent],
  templateUrl: './quotation-list.component.html',
  styleUrls: ['./quotation-list.component.scss'],
})
export class QuotationListComponent implements OnInit {
  readonly store = inject(QuotationStore);
  private companyContext = inject(CompanyContextService);
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'issueDate';
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
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : 'issueDate DESC',
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

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  isExpired(row: QuotationDto): boolean {
    if (row.status !== 'Submitted' || !row.validUntil) return false;
    return new Date(row.validUntil) < new Date();
  }

  isExpiringSoon(row: QuotationDto): boolean {
    if (row.status !== 'Submitted' || !row.validUntil) return false;
    const days = this.getDaysRemaining(row);
    return days > 0 && days <= 7;
  }

  getDaysRemaining(row: QuotationDto): number {
    if (!row.validUntil) return 999;
    const diff = new Date(row.validUntil).getTime() - new Date().getTime();
    return Math.ceil(diff / (1000 * 60 * 60 * 24));
  }
}
