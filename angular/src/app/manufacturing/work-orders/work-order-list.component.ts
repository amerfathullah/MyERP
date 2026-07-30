import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { WorkOrderStore } from '../store/work-order.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-work-order-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, PaginationComponent, SortableHeaderComponent],
  templateUrl: './work-order-list.component.html',
  styleUrls: ['./work-order-list.component.scss'],
})
export class WorkOrderListComponent implements OnInit {
  readonly store = inject(WorkOrderStore);
  private companyContext = inject(CompanyContextService);
  private localization = inject(LocalizationService);

  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'plannedStartDate';
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
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : 'plannedStartDate DESC',
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

  getStatusLabel(status: number): string {
    const keys = ['Draft', 'Submitted', 'NotStarted', 'InProcess', 'Completed', 'Stopped', 'Cancelled'];
    return this.localization.instant('::' + (keys[status] ?? 'Draft'));
  }

  isOverdue(row: any): boolean {
    if (!row.plannedEndDate) return false;
    const status = row.status ?? 0;
    if (status >= 4) return false; // Completed/Stopped/Cancelled
    return new Date(row.plannedEndDate) < new Date(new Date().toDateString());
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }
}
