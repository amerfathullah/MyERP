import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { DatePresetsComponent, type DateRange } from '../../shared/components/date-presets/date-presets.component';
import { PurchaseOrderStore } from '../store/purchase-order.store';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-purchase-order-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe,
    StatusBadgeComponent, PaginationComponent, SortableHeaderComponent, DatePresetsComponent],
  templateUrl: './purchase-order-list.component.html',
  styleUrls: ['./purchase-order-list.component.scss'],
})
export class PurchaseOrderListComponent implements OnInit {
  readonly store = inject(PurchaseOrderStore);
  private companyContext = inject(CompanyContextService);
  private poService = inject(PurchaseOrderService);
  displayedColumns = ['select', 'orderNumber', 'orderDate', 'grandTotal', 'status', 'actions'];
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  selectedIds = new Set<string>();
  isBulkProcessing = false;
  statusFilter = '';
  sortField: string | null = 'orderDate';
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
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : 'orderDate DESC',
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
    const data = this.store.entities().map(po => ({
      'Order #': po.orderNumber,
      'Date': po.orderDate,
      'Total': po.grandTotal,
      'Status': po.status,
    }));
    exportToCsv('purchase-orders.csv', data, ['Order #', 'Date', 'Total', 'Status']);
  }

  // ── Bulk Selection ──

  toggleSelect(id: string, checked: boolean): void {
    if (checked) this.selectedIds.add(id); else this.selectedIds.delete(id);
  }

  toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.store.entities().forEach(po => { if (po.id) this.selectedIds.add(po.id); });
    } else {
      this.selectedIds.clear();
    }
  }

  isAllSelected(): boolean {
    const entities = this.store.entities();
    return entities.length > 0 && entities.every(po => po.id && this.selectedIds.has(po.id));
  }

  clearSelection(): void {
    this.selectedIds.clear();
  }

  bulkSubmit(): void {
    const draftIds = this.store.entities()
      .filter(po => po.id && this.selectedIds.has(po.id) && po.status === 'Draft')
      .map(po => po.id!);

    if (draftIds.length === 0) return;

    this.isBulkProcessing = true;
    this.poService.bulkSubmit(draftIds).subscribe({
      next: () => {
        this.isBulkProcessing = false;
        this.selectedIds.clear();
        this.loadData();
      },
      error: () => { this.isBulkProcessing = false; },
    });
  }
}
