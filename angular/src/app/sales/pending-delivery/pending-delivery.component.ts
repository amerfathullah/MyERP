import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { PendingDeliveryService } from '../../proxy/sales/pending-delivery.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface PendingDeliveryItem {
  salesOrderId: string;
  orderNumber: string;
  orderDate: string;
  deliveryDate: string;
  customerId: string;
  customerName: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  orderedQty: number;
  deliveredQty: number;
  pendingQty: number;
  uom: string;
  rate: number;
  pendingAmount: number;
  daysUntilDue: number;
  isOverdue: boolean;
}

interface PendingDeliveryReport {
  asOfDate: string;
  totalOrders: number;
  totalItems: number;
  totalPendingAmount: number;
  overdueCount: number;
  overdueAmount: number;
  items: PendingDeliveryItem[];
}

@Component({
  selector: 'app-pending-delivery',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'PendingDeliveryReport' | abpLocalization">
      <!-- Filters -->
      <div class="card mb-3">
        <div class="card-body py-2">
          <div class="row align-items-end g-2">
            <div class="col-auto">
              <label class="form-label small mb-0">{{ 'Filter' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="overdueOnly" (change)="loadReport()">
                <option [ngValue]="false">{{ 'AllPending' | abpLocalization }}</option>
                <option [ngValue]="true">{{ 'OverdueOnly' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-auto">
              <label class="form-label small mb-0">{{ 'Customer' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="customerFilter" (change)="loadReport()">
                <option value="">{{ 'AllCustomers' | abpLocalization }}</option>
                @for (c of uniqueCustomers(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>
            <div class="col-auto ms-auto">
              <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()" [disabled]="!report()">
                <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Batch Action Bar -->
      @if (selectedCount() > 0) {
        <div class="alert alert-info d-flex align-items-center mb-3 py-2">
          <i class="fa fa-check-circle me-2"></i>
          <span class="flex-grow-1">
            <strong>{{ selectedCount() }}</strong> {{ 'ItemsSelected' | abpLocalization }}
            ({{ selectedCustomerCount() }} {{ 'Customers' | abpLocalization }})
          </span>
          <button class="btn btn-sm btn-success me-2" (click)="createDeliveryNotes()" [disabled]="isCreatingDN()">
            @if (isCreatingDN()) {
              <i class="fa fa-spinner fa-spin me-1"></i>
            } @else {
              <i class="fa fa-truck me-1"></i>
            }
            {{ 'CreateDeliveryNotes' | abpLocalization }}
          </button>
          <button class="btn btn-sm btn-outline-secondary" (click)="clearSelection()">
            <i class="fa fa-times me-1"></i>{{ 'ClearSelection' | abpLocalization }}
          </button>
        </div>
      }

      @if (loading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (report()) {
        <!-- KPI Cards -->
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card border-primary h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'TotalOrders' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-primary">{{ report()!.totalOrders }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-info h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'PendingItems' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-info">{{ report()!.totalItems }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-warning h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'TotalPendingAmount' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-warning">{{ report()!.totalPendingAmount | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-danger h-100">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'Overdue' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-danger">{{ report()!.overdueCount }} ({{ report()!.overdueAmount | number:'1.2-2' }})</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Items Table -->
        <div class="card">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th style="width: 30px;"><input type="checkbox" class="form-check-input" [checked]="isAllSelected()" (change)="toggleSelectAll($event)" /></th>
                    <th>{{ 'OrderNumber' | abpLocalization }}</th>
                    <th>{{ 'Customer' | abpLocalization }}</th>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th class="text-end">{{ 'PendingQty' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                    <th>{{ 'DeliveryDate' | abpLocalization }}</th>
                    <th>{{ 'Status' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of report()!.items; track item.salesOrderId + item.itemId) {
                    <tr [class.table-danger]="item.isOverdue" [class.table-warning]="item.daysUntilDue <= 3 && !item.isOverdue" [class.table-primary]="isSelected(item)">
                      <td><input type="checkbox" class="form-check-input" [checked]="isSelected(item)" (change)="toggleItem(item)" /></td>
                      <td>
                        <a [routerLink]="['/sales/orders', item.salesOrderId]" class="text-decoration-none">
                          {{ item.orderNumber }}
                        </a>
                      </td>
                      <td class="text-truncate" style="max-width: 150px;">{{ item.customerName }}</td>
                      <td>
                        <span class="fw-semibold">{{ item.itemCode }}</span>
                        <br><small class="text-muted">{{ item.itemName }}</small>
                      </td>
                      <td class="text-end">{{ item.pendingQty | number:'1.0-2' }} {{ item.uom }}</td>
                      <td class="text-end fw-semibold">{{ item.pendingAmount | number:'1.2-2' }}</td>
                      <td>{{ item.deliveryDate | date:'dd/MM/yyyy' }}</td>
                      <td>
                        @if (item.isOverdue) {
                          <span class="badge bg-danger">{{ item.daysUntilDue * -1 }}d overdue</span>
                        } @else if (item.daysUntilDue <= 3) {
                          <span class="badge bg-warning text-dark">{{ item.daysUntilDue }}d</span>
                        } @else {
                          <span class="badge bg-success">{{ item.daysUntilDue }}d</span>
                        }
                      </td>
                    </tr>
                  }
                  @if (!report()!.items.length) {
                    <tr><td colspan="8" class="text-center text-muted py-4">{{ 'NoPendingDeliveries' | abpLocalization }}</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      } @else {
        <div class="text-center py-5 text-muted">
          <i class="fa fa-truck fa-3x mb-3 opacity-25"></i>
          <p>{{ 'SelectCompanyToViewPendingDeliveries' | abpLocalization }}</p>
        </div>
      }
    </abp-page>
  `
})
export class PendingDeliveryComponent implements OnInit {
  private pendingDeliveryService = inject(PendingDeliveryService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  report = signal<PendingDeliveryReport | null>(null);
  loading = signal(false);
  isCreatingDN = signal(false);
  overdueOnly = false;
  customerFilter = '';

  // Selection state — key = salesOrderId::itemId
  private selectedItems = signal<Set<string>>(new Set());

  selectedCount = computed(() => this.selectedItems().size);
  selectedCustomerCount = computed(() => {
    const customers = new Set<string>();
    for (const key of this.selectedItems()) {
      const item = this.report()?.items.find(i => this.getKey(i) === key);
      if (item) customers.add(item.customerId);
    }
    return customers.size;
  });

  uniqueCustomers = computed(() => {
    if (!this.report()) return [];
    const map = new Map<string, string>();
    for (const item of this.report()!.items) {
      if (!map.has(item.customerId)) {
        map.set(item.customerId, item.customerName);
      }
    }
    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  });

  ngOnInit() {
    this.loadReport();
  }

  getKey(item: PendingDeliveryItem): string {
    return `${item.salesOrderId}::${item.itemId}`;
  }

  isSelected(item: PendingDeliveryItem): boolean {
    return this.selectedItems().has(this.getKey(item));
  }

  isAllSelected(): boolean {
    const items = this.report()?.items ?? [];
    return items.length > 0 && items.every(i => this.isSelected(i));
  }

  toggleItem(item: PendingDeliveryItem): void {
    const key = this.getKey(item);
    const current = new Set(this.selectedItems());
    if (current.has(key)) {
      current.delete(key);
    } else {
      current.add(key);
    }
    this.selectedItems.set(current);
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const items = this.report()?.items ?? [];
    if (checked) {
      this.selectedItems.set(new Set(items.map(i => this.getKey(i))));
    } else {
      this.selectedItems.set(new Set());
    }
  }

  clearSelection(): void {
    this.selectedItems.set(new Set());
  }

  createDeliveryNotes(): void {
    if (this.selectedCount() === 0) return;

    this.isCreatingDN.set(true);

    // Group selected items by customer (one DN per customer per ERPNext pattern)
    const byCustomer = new Map<string, { salesOrderId: string; itemId: string; quantity: number }[]>();
    for (const key of this.selectedItems()) {
      const item = this.report()?.items.find(i => this.getKey(i) === key);
      if (!item) continue;
      if (!byCustomer.has(item.customerId)) {
        byCustomer.set(item.customerId, []);
      }
      byCustomer.get(item.customerId)!.push({
        salesOrderId: item.salesOrderId,
        itemId: item.itemId,
        quantity: item.pendingQty
      });
    }

    const companyId = this.companyContext.currentCompanyId();
    const requests: { customerId: string; companyId: string; items: { salesOrderId: string; itemId: string; quantity: number }[] }[] = [];
    for (const [customerId, items] of byCustomer) {
      requests.push({ customerId, companyId: companyId!, items });
    }

    // Create DNs sequentially per customer (avoids race conditions on SO fulfillment counters)
    this.createDNsSequentially(requests, 0);
  }

  private createDNsSequentially(requests: any[], index: number): void {
    if (index >= requests.length) {
      this.isCreatingDN.set(false);
      this.toaster.success(this.l.instant('::DeliveryNotesCreated', requests.length.toString()));
      this.clearSelection();
      this.loadReport(); // Refresh to show updated pending quantities
      return;
    }

    this.pendingDeliveryService.createDeliveryNote(requests[index] as any).subscribe({
      next: () => {
        this.createDNsSequentially(requests, index + 1);
      },
      error: (err: any) => {
        this.isCreatingDN.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      }
    });
  }

  loadReport() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    const params: any = { companyId, overdueOnly: this.overdueOnly.toString() };
    if (this.customerFilter) params.customerId = this.customerFilter;

    this.pendingDeliveryService.getReport({ companyId, overdueOnly: this.overdueOnly, customerId: this.customerFilter || undefined } as any).subscribe({
      next: (data) => {
        this.report.set(data as any);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  exportCsv() {
    if (!this.report()) return;
    const rows = this.report()!.items.map(i => ({
      'Order #': i.orderNumber,
      'Customer': i.customerName,
      'Item Code': i.itemCode,
      'Item Name': i.itemName,
      'Pending Qty': i.pendingQty,
      'UOM': i.uom,
      'Rate': i.rate,
      'Pending Amount': i.pendingAmount,
      'Delivery Date': i.deliveryDate,
      'Days Until Due': i.daysUntilDue,
      'Overdue': i.isOverdue ? 'Yes' : 'No'
    }));
    exportToCsv('pending-deliveries', rows, ['Order #', 'Customer', 'Item Code', 'Item Name', 'Pending Qty', 'UOM', 'Rate', 'Pending Amount', 'Delivery Date', 'Days Until Due', 'Overdue']);
  }
}
