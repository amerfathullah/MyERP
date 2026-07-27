import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { HttpClient } from '@angular/common/http';
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
            <div class="col-auto ms-auto">
              <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()" [disabled]="!report()">
                <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

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
                    <tr [class.table-danger]="item.isOverdue" [class.table-warning]="item.daysUntilDue <= 3 && !item.isOverdue">
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
                    <tr><td colspan="7" class="text-center text-muted py-4">{{ 'NoPendingDeliveries' | abpLocalization }}</td></tr>
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
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  report = signal<PendingDeliveryReport | null>(null);
  loading = signal(false);
  overdueOnly = false;

  ngOnInit() {
    this.loadReport();
  }

  loadReport() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.http.get<PendingDeliveryReport>('/api/app/pending-delivery/report', {
      params: { companyId, overdueOnly: this.overdueOnly.toString() }
    }).subscribe({
      next: (data) => {
        this.report.set(data);
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
