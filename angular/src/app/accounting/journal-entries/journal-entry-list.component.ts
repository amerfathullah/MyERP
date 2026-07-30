import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { JournalEntryStore } from '../store/journal-entry.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { DatePresetsComponent, type DateRange } from '../../shared/components/date-presets/date-presets.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

const VOUCHER_TYPE_LABELS: Record<number, string> = {
  0: 'Journal Entry', 1: 'Inter Company', 2: 'Bank Entry', 3: 'Cash Entry',
  4: 'Credit Card', 5: 'Debit Note', 6: 'Credit Note', 7: 'Contra Entry',
  8: 'Excise Entry', 9: 'Write Off', 10: 'Opening Entry', 11: 'Depreciation',
  12: 'Revaluation', 13: 'Gain/Loss', 14: 'Deferred Revenue', 15: 'Deferred Expense',
  16: 'Reversal', 17: 'Period Closing',
};

@Component({
  selector: 'app-journal-entry-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    PaginationComponent,
    SortableHeaderComponent,
    DatePresetsComponent],
  templateUrl: './journal-entry-list.component.html',
  styleUrls: ['./journal-entry-list.component.scss'],
})
export class JournalEntryListComponent implements OnInit {
  readonly store = inject(JournalEntryStore);
  private companyContext = inject(CompanyContextService);

  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  voucherTypeFilter = '';
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
      voucherType: this.voucherTypeFilter || undefined,
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
    this.voucherTypeFilter = type;
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

  voucherTypeLabel(type: number | undefined): string {
    return VOUCHER_TYPE_LABELS[type ?? 0] ?? 'Journal Entry';
  }

  exportCsv(): void {
    const rows = this.store.entities().map(r => ({
      entryNumber: r.entryNumber ?? '',
      postingDate: r.postingDate,
      voucherType: this.voucherTypeLabel((r as any).voucherType),
      totalDebit: r.totalDebit,
      status: r.status,
    }));
    exportToCsv('journal-entries.csv', rows, ['entryNumber', 'postingDate', 'voucherType', 'totalDebit', 'status']);
  }
}
