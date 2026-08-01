import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PartyPerformanceService } from '../../proxy/core/party-performance.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-po-fulfillment',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h3><i class="fas fa-truck-loading me-2 text-primary"></i>{{ '::PoFulfillmentTracking' | abpLocalization }}</h3>
      <button class="btn btn-outline-secondary btn-sm" (click)="exportCsv()" [disabled]="!report()">
        <i class="fas fa-download me-1"></i> {{ '::ExportCSV' | abpLocalization }}
      </button>
    </div>

    <!-- Filter Row -->
    <div class="card mb-4">
      <div class="card-body py-2">
        <div class="row g-2 align-items-end">
          <div class="col-md-3">
            <label class="form-label small">{{ '::Supplier' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="supplierFilter" (change)="loadReport()">
              <option value="">{{ '::All' | abpLocalization }}</option>
              @for (s of suppliers(); track s.id) {
                <option [value]="s.id">{{ s.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label small">{{ '::Status' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="statusFilter" (change)="applyFilter()">
              <option value="">{{ '::All' | abpLocalization }}</option>
              <option value="Ordered">{{ '::Ordered' | abpLocalization }}</option>
              <option value="PartiallyReceived">{{ '::PartiallyReceived' | abpLocalization }}</option>
              <option value="FullyReceived">{{ '::FullyReceived' | abpLocalization }}</option>
              <option value="Overdue">{{ '::Overdue' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm w-100" (click)="loadReport()" [disabled]="loading()">
              @if (loading()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              {{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>
      </div>
    </div>

    @if (report(); as r) {
      <!-- KPI Cards -->
      <div class="row mb-4">
        <div class="col-sm-3">
          <div class="card text-center">
            <div class="card-body py-2">
              <div class="fs-4 fw-bold">{{ r.totalItems }}</div>
              <div class="text-muted small">{{ '::TotalItems' | abpLocalization }}</div>
            </div>
          </div>
        </div>
        <div class="col-sm-3">
          <div class="card text-center">
            <div class="card-body py-2">
              <div class="fs-4 fw-bold text-warning">{{ r.pendingReceiptItems }}</div>
              <div class="text-muted small">{{ '::PendingReceipt' | abpLocalization }}</div>
            </div>
          </div>
        </div>
        <div class="col-sm-3">
          <div class="card text-center">
            <div class="card-body py-2">
              <div class="fs-4 fw-bold text-info">{{ r.pendingBillingItems }}</div>
              <div class="text-muted small">{{ '::PendingBilling' | abpLocalization }}</div>
            </div>
          </div>
        </div>
        <div class="col-sm-3">
          <div class="card text-center">
            <div class="card-body py-2">
              <div class="fs-4 fw-bold text-danger">{{ r.overdueItems }}</div>
              <div class="text-muted small">{{ '::OverdueItems' | abpLocalization }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Fulfillment Table -->
      @if (filteredItems().length > 0) {
        <div class="card">
          <div class="table-responsive">
            <table class="table table-hover table-sm mb-0">
              <thead>
                <tr>
                  <th>{{ '::OrderNumber' | abpLocalization }}</th>
                  <th>{{ '::Supplier' | abpLocalization }}</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Ordered' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Received' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Billed' | abpLocalization }}</th>
                  <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::ExpectedDate' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of filteredItems(); track $index) {
                  <tr [class.table-danger]="item.isOverdue" [class.table-warning]="item.fulfillmentStatus === 'PartiallyReceived'">
                    <td>
                      <a [routerLink]="['/purchasing/orders', item.purchaseOrderId]" class="text-decoration-none">
                        {{ item.orderNumber }}
                      </a>
                    </td>
                    <td>{{ item.supplierName }}</td>
                    <td>{{ item.itemName }}</td>
                    <td class="text-end">{{ item.orderedQty }}</td>
                    <td class="text-end">
                      {{ item.receivedQty }}
                      @if (item.pendingReceiptQty > 0) {
                        <small class="text-warning ms-1">({{ item.pendingReceiptQty }} {{ '::Pending' | abpLocalization }})</small>
                      }
                    </td>
                    <td class="text-end">
                      {{ item.billedQty }}
                      @if (item.pendingBillingQty > 0) {
                        <small class="text-info ms-1">({{ item.pendingBillingQty }} {{ '::Pending' | abpLocalization }})</small>
                      }
                    </td>
                    <td class="text-center">
                      <span class="badge" [class.bg-secondary]="item.fulfillmentStatus === 'Ordered'"
                                         [class.bg-warning]="item.fulfillmentStatus === 'PartiallyReceived'"
                                         [class.bg-info]="item.fulfillmentStatus === 'FullyReceived'"
                                         [class.bg-success]="item.fulfillmentStatus === 'FullyBilled'">
                        {{ ('::' + item.fulfillmentStatus) | abpLocalization }}
                      </span>
                      @if (item.isOverdue) {
                        <span class="badge bg-danger ms-1">{{ item.daysOverdue }}d {{ '::Overdue' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      @if (item.expectedDeliveryDate) {
                        <span [class.text-danger]="item.isOverdue">{{ item.expectedDeliveryDate | date:'dd/MM/yyyy' }}</span>
                      } @else { — }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      } @else {
        <div class="card">
          <div class="card-body text-center py-5 text-muted">
            <i class="fas fa-check-circle fa-3x mb-3 text-success"></i>
            <p>{{ '::AllOrdersFulfilled' | abpLocalization }}</p>
          </div>
        </div>
      }
    }
  `,
})
export class PoFulfillmentComponent implements OnInit {
  private partyPerformanceService = inject(PartyPerformanceService);
  private supplierService = inject(SupplierService);
  private companyContext = inject(CompanyContextService);

  report = signal<any>(null);
  loading = signal(false);
  suppliers = signal<{ id: string; name: string }[]>([]);
  supplierFilter = '';
  statusFilter = '';

  ngOnInit() {
    this.loadSuppliers();
    this.loadReport();
  }

  private loadSuppliers() {
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (res) => this.suppliers.set((res.items ?? []).map((s: any) => ({ id: s.id, name: s.name }))),
      error: () => {},
    });
  }

  loadReport() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    const params: any = { companyId };
    if (this.supplierFilter) params.supplierId = this.supplierFilter;

    this.partyPerformanceService.getPoFulfillmentReport(companyId, this.supplierFilter || undefined).subscribe({
      next: (data: any) => { this.report.set(data); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  filteredItems(): any[] {
    const items = this.report()?.items ?? [];
    if (!this.statusFilter) return items;
    if (this.statusFilter === 'Overdue') return items.filter((i: any) => i.isOverdue);
    return items.filter((i: any) => i.fulfillmentStatus === this.statusFilter);
  }

  applyFilter() {
    // Trigger view update (signal-based filtering)
  }

  exportCsv() {
    const items = this.filteredItems();
    const mapped = items.map((i: any) => ({
      'PO Number': i.orderNumber,
      'Supplier': i.supplierName,
      'Item': i.itemName,
      'Ordered': i.orderedQty,
      'Received': i.receivedQty,
      'Billed': i.billedQty,
      'Pending Receipt': i.pendingReceiptQty,
      'Pending Billing': i.pendingBillingQty,
      'Status': i.fulfillmentStatus,
      'Days Overdue': i.daysOverdue,
    }));
    exportToCsv('po-fulfillment-report.csv', mapped, [
      'PO Number', 'Supplier', 'Item', 'Ordered', 'Received', 'Billed',
      'Pending Receipt', 'Pending Billing', 'Status', 'Days Overdue',
    ]);
  }
}
