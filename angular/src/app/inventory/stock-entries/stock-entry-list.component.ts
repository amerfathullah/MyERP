import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { RouterModule } from '@angular/router';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { StockEntryStore } from '../store/stock-entry.store';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { DatePresetsComponent, type DateRange } from '../../shared/components/date-presets/date-presets.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-stock-entry-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, PageModule, LocalizationPipe,
    RouterModule, StatusBadgeComponent, PaginationComponent, SortableHeaderComponent, DatePresetsComponent],
  templateUrl: './stock-entry-list.component.html',
  styleUrls: ['./stock-entry-list.component.scss'],
})
export class StockEntryListComponent implements OnInit {
  readonly store = inject(StockEntryStore);
  private companyContext = inject(CompanyContextService);

  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  entryTypeFilter = '';
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
      entryType: this.entryTypeFilter || undefined,
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

  onTypeChange(type: string): void {
    this.entryTypeFilter = type;
    this.currentPage = 0;
    this.loadData();
  }

  onSort(event: SortEvent): void {
    this.sortField = event.field;
    this.sortDirection = event.direction;
    this.currentPage = 0;
    this.loadData();
  }

  onDateChange(): void {
    this.currentPage = 0;
    this.loadData();
  }

  onDatePreset(range: DateRange): void {
    this.fromDate = range.from;
    this.toDate = range.to;
    this.currentPage = 0;
    this.loadData();
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }
}
