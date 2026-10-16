import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ProcurementTrackerService } from '../../../proxy/purchasing/procurement-tracker.service';
import {
  ProcurementTrackerFilterDto,
  ProcurementTrackerReportDto,
  ProcurementTrackerRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  selector: 'app-procurement-tracker',
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-route text-primary me-2"></i>
            {{ '::ProcurementTracker' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::ProcurementTrackerDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows && report()!.rows!.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::From' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::To' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-4">
              <div class="form-check mt-3">
                <input
                  class="form-check-input"
                  type="checkbox"
                  id="showCompletedOrders"
                  [(ngModel)]="showCompletedOrders"
                  (change)="loadReport()"
                />
                <label class="form-check-label small" for="showCompletedOrders">
                  {{ '::ShowCompletedOrders' | abpLocalization }}
                </label>
              </div>
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-primary btn-sm w-100"
                (click)="loadReport()"
                [disabled]="isLoading()"
              >
                <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      @if (report()?.summary; as s) {
        <div class="row g-3 mb-3">
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::TotalRequisitions' | abpLocalization }}</div>
                <h4 class="fw-bold text-dark mb-0">{{ s.totalRequisitions || 0 }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::TotalOrders' | abpLocalization }}</div>
                <h4 class="fw-bold text-dark mb-0">{{ s.totalOrders || 0 }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::EstimatedCost' | abpLocalization }}</div>
                <h4 class="fw-bold text-info mb-0">{{ (s.totalEstimatedCost || 0) | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::PurchaseOrderAmount' | abpLocalization }}</div>
                <h4 class="fw-bold text-primary mb-0">{{ (s.totalPurchaseOrderAmount || 0) | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::ActualCost' | abpLocalization }}</div>
                <h4 class="fw-bold text-success mb-0">{{ (s.totalActualCost || 0) | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (isLoading()) {
            <div class="text-center py-5">
              <i class="fa fa-spinner fa-spin fa-2x text-primary mb-2"></i>
              <div class="text-muted small">{{ '::Loading' | abpLocalization }}</div>
            </div>
          } @else if (report()?.rows && report()!.rows!.length > 0) {
            <div class="table-responsive">
              <table class="table table-hover table-striped table-bordered align-middle mb-0 small">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Date' | abpLocalization }} (MR)</th>
                    <th>{{ '::MaterialRequestNo' | abpLocalization }}</th>
                    <th>{{ '::Requestor' | abpLocalization }}</th>
                    <th>{{ '::RequestingSite' | abpLocalization }}</th>
                    <th>{{ '::CostCenter' | abpLocalization }}</th>
                    <th>{{ '::Project' | abpLocalization }}</th>
                    <th>{{ '::ItemCode' | abpLocalization }}</th>
                    <th>{{ '::ItemName' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                    <th>{{ '::Uom' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                    <th>{{ '::Date' | abpLocalization }} (PO)</th>
                    <th>{{ '::PurchaseOrder' | abpLocalization }}</th>
                    <th>{{ '::Supplier' | abpLocalization }}</th>
                    <th class="text-end">{{ '::EstimatedCost' | abpLocalization }}</th>
                    <th class="text-end">{{ '::PurchaseOrderAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ActualCost' | abpLocalization }}</th>
                    <th>{{ '::ExpectedDelivery' | abpLocalization }}</th>
                    <th>{{ '::ActualDeliveryDate' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows!; track $index) {
                    <tr>
                      <td>{{ row.materialRequestDate ? (row.materialRequestDate | date:'mediumDate') : '-' }}</td>
                      <td class="fw-medium">{{ row.materialRequestNo || '-' }}</td>
                      <td>{{ row.requestor || '-' }}</td>
                      <td>{{ row.requestingSite || '-' }}</td>
                      <td>{{ row.costCenter || '-' }}</td>
                      <td>{{ row.project || '-' }}</td>
                      <td><code>{{ row.itemCode }}</code></td>
                      <td>{{ row.itemName }}</td>
                      <td class="text-end fw-bold">{{ row.quantity | number:'1.2-2' }}</td>
                      <td>{{ row.unitOfMeasurement }}</td>
                      <td class="text-center">
                        <span
                          class="badge"
                          [class.bg-secondary]="row.status === 'Draft'"
                          [class.bg-info]="row.status === 'Submitted' || row.status === 'Open'"
                          [class.bg-primary]="row.status === 'ToReceive' || row.status === 'ToBill'"
                          [class.bg-success]="row.status === 'Completed'"
                          [class.bg-dark]="row.status === 'Closed'"
                          [class.bg-danger]="row.status === 'Cancelled'"
                        >
                          {{ row.status }}
                        </span>
                      </td>
                      <td>{{ row.purchaseOrderDate ? (row.purchaseOrderDate | date:'mediumDate') : '-' }}</td>
                      <td class="fw-medium text-primary">{{ row.purchaseOrderNo || '-' }}</td>
                      <td>{{ row.supplierName || '-' }}</td>
                      <td class="text-end">{{ row.estimatedCost | number:'1.2-2' }}</td>
                      <td class="text-end fw-medium text-primary">{{ row.purchaseOrderAmount | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold text-success">{{ row.actualCost | number:'1.2-2' }}</td>
                      <td>{{ row.expectedDeliveryDate ? (row.expectedDeliveryDate | date:'mediumDate') : '-' }}</td>
                      <td>
                        @if (row.actualDeliveryDate) {
                          <span class="text-success">{{ row.actualDeliveryDate | date:'mediumDate' }}</span>
                        } @else {
                          <span class="text-muted">-</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-search fa-3x mb-3 text-secondary opacity-50"></i>
              <p class="mb-0">{{ '::NoProcurementRecordsFound' | abpLocalization }}</p>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class ProcurementTrackerComponent implements OnInit {
  private service = inject(ProcurementTrackerService);
  private companyContext = inject(CompanyContextService);

  report = signal<ProcurementTrackerReportDto | null>(null);
  isLoading = signal<boolean>(false);

  fromDate: string = '';
  toDate: string = '';
  showCompletedOrders: boolean = false;

  ngOnInit(): void {
    const today = new Date();
    const oneMonthAgo = new Date();
    oneMonthAgo.setMonth(today.getMonth() - 1);

    this.fromDate = oneMonthAgo.toISOString().substring(0, 10);
    this.toDate = today.toISOString().substring(0, 10);

    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    const filter: ProcurementTrackerFilterDto = {
      companyId,
      fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
      toDate: this.toDate ? new Date(this.toDate).toISOString() : undefined,
      showCompletedOrders: this.showCompletedOrders,
    };

    this.service.getReport(filter).subscribe({
      next: res => {
        this.report.set(res);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }

  exportCsv(): void {
    const data = this.report()?.rows;
    if (!data || data.length === 0) return;

    const exportRows = data.map(r => ({
      'Material Request Date': r.materialRequestDate ? r.materialRequestDate.substring(0, 10) : '',
      'Material Request No': r.materialRequestNo || '',
      Requestor: r.requestor || '',
      'Requesting Site': r.requestingSite || '',
      'Cost Center': r.costCenter || '',
      Project: r.project || '',
      'Item Code': r.itemCode,
      'Item Name': r.itemName,
      Quantity: r.quantity,
      UOM: r.unitOfMeasurement,
      Status: r.status,
      'Purchase Order Date': r.purchaseOrderDate ? r.purchaseOrderDate.substring(0, 10) : '',
      'Purchase Order No': r.purchaseOrderNo || '',
      Supplier: r.supplierName || '',
      'Estimated Cost': r.estimatedCost,
      'PO Amount': r.purchaseOrderAmount,
      'Actual Cost': r.actualCost,
      'Expected Delivery Date': r.expectedDeliveryDate ? r.expectedDeliveryDate.substring(0, 10) : '',
      'Actual Delivery Date': r.actualDeliveryDate ? r.actualDeliveryDate.substring(0, 10) : '',
    }));

    const columns = [
      'Material Request Date',
      'Material Request No',
      'Requestor',
      'Requesting Site',
      'Cost Center',
      'Project',
      'Item Code',
      'Item Name',
      'Quantity',
      'UOM',
      'Status',
      'Purchase Order Date',
      'Purchase Order No',
      'Supplier',
      'Estimated Cost',
      'PO Amount',
      'Actual Cost',
      'Expected Delivery Date',
      'Actual Delivery Date',
    ];

    exportToCsv(
      `procurement-tracker-${new Date().toISOString().substring(0, 10)}.csv`,
      exportRows,
      columns
    );
  }
}
