import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PendingSoItemsForPurchaseRequestService } from '../../../proxy/sales/pending-so-items-for-purchase-request.service';
import type { PendingSoItemsForPurchaseRequestReportDto } from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-pending-so-items-for-purchase-request',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0"><i class="fas fa-boxes-packing me-2 text-primary"></i>{{ '::PendingSoItemsForPurchaseRequest' | abpLocalization }}</h5>
          <small class="text-muted">{{ '::PendingSoItemsForPurchaseRequestDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (data()) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fas fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4">
          <div class="col-md-3">
            <label class="form-label small">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-3">
            <label class="form-label small">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm w-100" (click)="loadReport()" [disabled]="isLoading()">
              <i class="fas fa-sync-alt me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
        } @else if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-2">
              <div class="card text-center border-primary h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Sales Orders</div>
                <div class="fs-4 fw-bold text-primary">{{ d.totalSalesOrders }}</div>
              </div></div>
            </div>
            <div class="col-md-2">
              <div class="card text-center border-info h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Distinct Items</div>
                <div class="fs-4 fw-bold text-info">{{ d.totalItems }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-secondary h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Total {{ '::SoQuantity' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-secondary">{{ d.totalSoQuantity | number:'1.2-2' }}</div>
              </div></div>
            </div>
            <div class="col-md-3">
              <div class="card text-center border-success h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Total {{ '::RequestedQuantity' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-success">{{ d.totalRequestedQuantity | number:'1.2-2' }}</div>
              </div></div>
            </div>
            <div class="col-md-2">
              <div class="card text-center border-danger h-100 bg-light"><div class="card-body py-3">
                <div class="text-muted small">Total {{ '::PendingQuantity' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-danger">{{ d.totalPendingQuantity | number:'1.2-2' }}</div>
              </div></div>
            </div>
          </div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::SalesOrder' | abpLocalization }}</th>
                  <th>{{ '::Customer' | abpLocalization }}</th>
                  <th>Order Date</th>
                  <th>Material Requests</th>
                  <th class="text-end">{{ '::SoQuantity' | abpLocalization }}</th>
                  <th class="text-end">{{ '::RequestedQuantity' | abpLocalization }}</th>
                  <th class="text-end text-danger">{{ '::PendingQuantity' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.salesOrderId + '-' + row.itemId) {
                  <tr>
                    <td>
                      <div>
                        <strong>{{ row.itemName }}</strong>
                        @if (row.isProductBundleComponent) {
                          <span class="badge bg-info text-dark ms-1">Bundle Component</span>
                        }
                      </div>
                      <small class="text-muted">{{ row.itemCode }}</small>
                    </td>
                    <td><strong>{{ row.salesOrderNumber }}</strong></td>
                    <td>{{ row.customerName }}</td>
                    <td><small class="text-muted">{{ row.orderDate | date:'shortDate' }}</small></td>
                    <td>
                      @if (row.materialRequests) {
                        <small class="badge bg-light text-dark border">{{ row.materialRequests }}</small>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-end">{{ row.soQuantity | number:'1.2-2' }}</td>
                    <td class="text-end text-success">{{ row.requestedQuantity | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold text-danger">{{ row.pendingQuantity | number:'1.2-2' }}</td>
                  </tr>
                }
                @if (d.rows.length === 0) {
                  <tr>
                    <td colspan="8" class="text-center py-4 text-muted">
                      <i class="fas fa-check-circle text-success me-1"></i>
                      All Sales Order items have been fully requested in Material Requests!
                    </td>
                  </tr>
                }
              </tbody>
              @if (d.rows.length > 0) {
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td colspan="5">{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">{{ d.totalSoQuantity | number:'1.2-2' }}</td>
                    <td class="text-end text-success">{{ d.totalRequestedQuantity | number:'1.2-2' }}</td>
                    <td class="text-end text-danger">{{ d.totalPendingQuantity | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              }
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class PendingSoItemsForPurchaseRequestComponent implements OnInit {
  private service = inject(PendingSoItemsForPurchaseRequestService);
  private companyContext = inject(CompanyContextService);

  data = signal<PendingSoItemsForPurchaseRequestReportDto | null>(null);
  isLoading = signal(false);

  fromDate: string = '';
  toDate: string = '';

  ngOnInit(): void {
    const today = new Date();
    const firstDayOfYear = new Date(today.getFullYear(), 0, 1);
    this.fromDate = firstDayOfYear.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getReport({
        companyId,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : null,
        toDate: this.toDate ? new Date(this.toDate).toISOString() : null,
      })
      .subscribe({
        next: (res) => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows.length) return;

    const exportRows = d.rows.map((r) => ({
      itemCode: r.itemCode,
      itemName: r.itemName,
      description: r.description ?? '',
      salesOrderNumber: r.salesOrderNumber,
      orderDate: r.orderDate,
      customerName: r.customerName,
      materialRequests: r.materialRequests,
      soQuantity: r.soQuantity,
      requestedQuantity: r.requestedQuantity,
      pendingQuantity: r.pendingQuantity,
      isBundleComponent: r.isProductBundleComponent ? 'Yes' : 'No',
    }));

    exportToCsv(
      `Pending_SO_Items_Purchase_${new Date().toISOString().split('T')[0]}.csv`,
      exportRows,
      [
        'itemCode',
        'itemName',
        'description',
        'salesOrderNumber',
        'orderDate',
        'customerName',
        'materialRequests',
        'soQuantity',
        'requestedQuantity',
        'pendingQuantity',
        'isBundleComponent',
      ]
    );
  }
}
