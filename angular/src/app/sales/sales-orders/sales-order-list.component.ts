import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesOrderStore } from '../store/sales-order.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { DatePresetsComponent, type DateRange } from '../../shared/components/date-presets/date-presets.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationService } from '@abp/ng.core';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-sales-order-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, PaginationComponent, SortableHeaderComponent, DatePresetsComponent],
  templateUrl: './sales-order-list.component.html',
  styleUrls: ['./sales-order-list.component.scss'],
})
export class SalesOrderListComponent implements OnInit {
  readonly store = inject(SalesOrderStore);
  private companyContext = inject(CompanyContextService);
  private soService = inject(SalesOrderService);
  private conversionService = inject(DocumentConversionService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  displayedColumns = ['select', 'orderNumber', 'orderDate', 'grandTotal', 'status', 'actions'];
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'orderDate';
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
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : 'orderDate DESC',
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
      companyId: this.companyContext.currentCompanyId() || undefined,
    });
  }

  onSearch(): void {
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

  onStatusChange(): void {
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
    const data = this.store.entities().map(o => ({
      'Order #': o.orderNumber,
      'Date': o.orderDate,
      'Total': o.grandTotal,
      'Status': o.status,
    }));
    exportToCsv('sales-orders.csv', data, ['Order #', 'Date', 'Total', 'Status']);
  }

  // ── Bulk Selection ──

  toggleSelect(id: string, checked: boolean): void {
    if (checked) this.selectedIds.add(id); else this.selectedIds.delete(id);
  }

  toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.store.entities().forEach(o => { if (o.id) this.selectedIds.add(o.id); });
    } else {
      this.selectedIds.clear();
    }
  }

  isAllSelected(): boolean {
    const entities = this.store.entities();
    return entities.length > 0 && entities.every(o => o.id && this.selectedIds.has(o.id));
  }

  clearSelection(): void {
    this.selectedIds.clear();
  }

  bulkSubmit(): void {
    const draftIds = this.store.entities()
      .filter(o => o.id && this.selectedIds.has(o.id) && o.status === 'Draft')
      .map(o => o.id!);
    if (draftIds.length === 0) return;

    this.isBulkProcessing = true;
    this.soService.bulkSubmit(draftIds).subscribe({
      next: () => {
        this.isBulkProcessing = false;
        this.selectedIds.clear();
        this.loadData();
      },
      error: () => { this.isBulkProcessing = false; },
    });
  }

  bulkCreateDN(): void {
    const activeIds = this.store.entities()
      .filter(o => o.id && this.selectedIds.has(o.id) &&
        ['ToDeliverAndBill', 'ToDeliver'].includes(o.status ?? ''))
      .map(o => o.id!);
    if (activeIds.length === 0) {
      this.toaster.info(this.l.instant('::NoOrdersReadyForDelivery'));
      return;
    }

    this.isBulkProcessing = true;
    let created = 0;
    let failed = 0;
    let remaining = activeIds.length;

    for (const soId of activeIds) {
      this.conversionService.convertSalesOrderToDeliveryNote(soId).subscribe({
        next: () => { created++; this.checkBulkDNComplete(--remaining, created, failed); },
        error: () => { failed++; this.checkBulkDNComplete(--remaining, created, failed); },
      });
    }
  }

  private checkBulkDNComplete(remaining: number, created: number, failed: number): void {
    if (remaining > 0) return;
    this.isBulkProcessing = false;
    this.selectedIds.clear();
    if (created > 0) {
      this.toaster.success(`${created} ${this.l.instant('::DeliveryNotesCreated')}`);
    }
    if (failed > 0) {
      this.toaster.warn(`${failed} ${this.l.instant('::OrdersSkipped')}`);
    }
    this.loadData();
  }

  bulkCreateSI(): void {
    const billableIds = this.store.entities()
      .filter(o => o.id && this.selectedIds.has(o.id) &&
        ['ToDeliverAndBill', 'ToBill'].includes(o.status ?? ''))
      .map(o => o.id!);
    if (billableIds.length === 0) {
      this.toaster.info(this.l.instant('::NoOrdersReadyForBilling'));
      return;
    }

    this.isBulkProcessing = true;
    let created = 0;
    let failed = 0;
    let remaining = billableIds.length;

    for (const soId of billableIds) {
      this.conversionService.convertSalesOrderToSalesInvoice(soId).subscribe({
        next: () => { created++; this.checkBulkSIComplete(--remaining, created, failed); },
        error: () => { failed++; this.checkBulkSIComplete(--remaining, created, failed); },
      });
    }
  }

  private checkBulkSIComplete(remaining: number, created: number, failed: number): void {
    if (remaining > 0) return;
    this.isBulkProcessing = false;
    this.selectedIds.clear();
    if (created > 0) {
      this.toaster.success(`${created} ${this.l.instant('::InvoicesCreated')}`);
    }
    if (failed > 0) {
      this.toaster.warn(`${failed} ${this.l.instant('::OrdersSkipped')}`);
    }
    this.loadData();
  }

  bulkClose(): void {
    const closableStatuses = ['ToDeliverAndBill', 'ToDeliver', 'ToBill', 'Completed'];
    const closableIds = this.store.entities()
      .filter(o => o.id && this.selectedIds.has(o.id) && closableStatuses.includes(o.status ?? ''))
      .map(o => o.id!);
    if (closableIds.length === 0) {
      this.toaster.info(this.l.instant('::NoOrdersReadyToClose'));
      return;
    }

    this.isBulkProcessing = true;
    let closed = 0;
    let failed = 0;
    let remaining = closableIds.length;

    for (const soId of closableIds) {
      this.soService.close(soId).subscribe({
        next: () => { closed++; this.checkBulkCloseComplete(--remaining, closed, failed); },
        error: () => { failed++; this.checkBulkCloseComplete(--remaining, closed, failed); },
      });
    }
  }

  private checkBulkCloseComplete(remaining: number, created: number, failed: number): void {
    if (remaining > 0) return;
    this.isBulkProcessing = false;
    this.selectedIds.clear();
    if (created > 0) {
      this.toaster.success(`${created} ${this.l.instant('::SuccessfullyClosed')}`);
    }
    if (failed > 0) {
      this.toaster.warn(`${failed} ${this.l.instant('::OrdersSkipped')}`);
    }
    this.loadData();
  }

  isDeliveryOverdue(row: any): boolean {
    if (!row.deliveryDate) return false;
    const active = row.status === 'ToDeliverAndBill' || row.status === 'ToDeliver';
    if (!active) return false;
    const dd = new Date(row.deliveryDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return dd < today;
  }
}
