import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { SupplierService } from '../../../proxy/purchasing/supplier.service';
import { SubcontractingInwardReportService } from '../../../proxy/purchasing/subcontracting-inward-report.service';
import type {
  SubcontractedItemsToBeDeliveredReportDto,
  SubcontractedRawMaterialsToBeReceivedReportDto,
  SubcontractingInwardOrderSummaryReportDto,
  SubcontractingInwardReportFilterDto,
  SupplierDto,
} from '../../../proxy/purchasing/models';

type ReportTab = 'items' | 'materials' | 'summary';

@Component({
  selector: 'app-subcontracting-inward-reports',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, PageModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="'SubcontractingInwardReports' | abpLocalization">
      <!-- Header Controls -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div class="d-flex align-items-center gap-2">
          <a routerLink="/purchasing/subcontracting-inward" class="btn btn-outline-secondary btn-sm">
            <i class="fa fa-arrow-left me-1"></i> {{ 'Back' | abpLocalization }}
          </a>
          <h4 class="mb-0">{{ 'SubcontractingInwardReports' | abpLocalization }}</h4>
        </div>
        <button class="btn btn-outline-secondary btn-sm" (click)="exportCsv()" [disabled]="loading()">
          <i class="fa fa-download me-1"></i> {{ 'Export' | abpLocalization }} CSV
        </button>
      </div>

      <!-- Filter Card -->
      <div class="card mb-4">
        <div class="card-body py-3">
          <div class="row g-2 align-items-end">
            <div class="col-md-2">
              <label class="form-label small text-muted mb-1">{{ 'FromDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
            </div>
            <div class="col-md-2">
              <label class="form-label small text-muted mb-1">{{ 'ToDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
            </div>
            <div class="col-md-3">
              <label class="form-label small text-muted mb-1">{{ 'Supplier' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="selectedSupplierId">
                <option value="">{{ 'All' | abpLocalization }}</option>
                @for (s of suppliers(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small text-muted mb-1">{{ 'Status' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="selectedStatus">
                <option value="">{{ 'AllStatuses' | abpLocalization }}</option>
                <option value="Open">Open</option>
                <option value="PartiallyReceived">Partially Received</option>
                <option value="Completed">Completed</option>
                <option value="Closed">Closed</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </div>
            <div class="col-md-2">
              <button class="btn btn-primary btn-sm w-100" (click)="loadActiveReport()" [disabled]="loading()">
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                }
                <i class="fa fa-filter me-1"></i> {{ 'Filter' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <ul class="nav nav-tabs mb-3">
        <li class="nav-item">
          <button
            class="nav-link"
            [class.active]="activeTab() === 'items'"
            (click)="switchTab('items')"
          >
            <i class="fa fa-boxes-stacked me-1"></i> {{ 'ItemsToBeDelivered' | abpLocalization }}
          </button>
        </li>
        <li class="nav-item">
          <button
            class="nav-link"
            [class.active]="activeTab() === 'materials'"
            (click)="switchTab('materials')"
          >
            <i class="fa fa-cubes-stacked me-1"></i> {{ 'RawMaterialsToBeReceived' | abpLocalization }}
          </button>
        </li>
        <li class="nav-item">
          <button
            class="nav-link"
            [class.active]="activeTab() === 'summary'"
            (click)="switchTab('summary')"
          >
            <i class="fa fa-table-list me-1"></i> {{ 'OrderSummary' | abpLocalization }}
          </button>
        </li>
      </ul>

      @if (loading()) {
        <div class="text-center py-5">
          <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
          <p class="mt-2 text-muted">{{ 'Loading' | abpLocalization }}...</p>
        </div>
      } @else {
        <!-- TAB 1: Items to be Delivered -->
        @if (activeTab() === 'items') {
          @if (itemsReport(); as rep) {
            <!-- KPI Cards -->
            <div class="row mb-3 g-2">
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-4 fw-bold text-primary">{{ rep.totalOrderQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'OrderQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-4 fw-bold text-info">{{ rep.totalProducedQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'ProducedQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-4 fw-bold text-success">{{ rep.totalDeliveredQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'DeliveredQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-4 fw-bold text-warning">{{ rep.totalPendingQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'PendingQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Table -->
            <div class="card shadow-sm">
              <div class="table-responsive">
                <table class="table table-hover table-sm align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ 'OrderNumber' | abpLocalization }}</th>
                      <th>{{ 'Date' | abpLocalization }}</th>
                      <th>{{ 'Supplier' | abpLocalization }}</th>
                      <th>{{ 'ItemCode' | abpLocalization }}</th>
                      <th>{{ 'ItemName' | abpLocalization }}</th>
                      <th>{{ 'Uom' | abpLocalization }}</th>
                      <th class="text-end">{{ 'OrderQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'ProducedQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'DeliveredQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'PendingQty' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of rep.rows ?? []; track $index) {
                      <tr>
                        <td>
                          <a [routerLink]="['/purchasing/subcontracting-inward', row.subcontractingInwardOrderId]">
                            {{ row.orderNumber }}
                          </a>
                        </td>
                        <td>{{ row.orderDate | date:'dd/MM/yyyy' }}</td>
                        <td>{{ row.partyName }}</td>
                        <td><code>{{ row.itemCode }}</code></td>
                        <td>{{ row.itemName }}</td>
                        <td>{{ row.uom }}</td>
                        <td class="text-end">{{ row.orderQty | number:'1.2-2' }}</td>
                        <td class="text-end">{{ row.producedQty | number:'1.2-2' }}</td>
                        <td class="text-end text-success">{{ row.deliveredQty | number:'1.2-2' }}</td>
                        <td class="text-end text-warning fw-semibold">{{ row.pendingQty | number:'1.2-2' }}</td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="10" class="text-center py-4 text-muted">
                          {{ 'NoDataAvailable' | abpLocalization }}
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        }

        <!-- TAB 2: Raw Materials to be Received -->
        @if (activeTab() === 'materials') {
          @if (materialsReport(); as rep) {
            <!-- KPI Cards -->
            <div class="row mb-3 g-2">
              <div class="col-sm-2">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-5 fw-bold text-primary">{{ rep.totalRequiredQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'RequiredQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-2">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-5 fw-bold text-danger">{{ rep.totalProcessLossQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'ProcessLossQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-5 fw-bold text-success">{{ rep.totalReceivedQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'ReceivedQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-2">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-5 fw-bold text-secondary">{{ rep.totalReturnedQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'ReturnedQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-5 fw-bold text-warning">{{ rep.totalPendingQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'PendingQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Table -->
            <div class="card shadow-sm">
              <div class="table-responsive">
                <table class="table table-hover table-sm align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ 'OrderNumber' | abpLocalization }}</th>
                      <th>{{ 'Date' | abpLocalization }}</th>
                      <th>{{ 'Supplier' | abpLocalization }}</th>
                      <th>{{ 'FinishedGood' | abpLocalization }}</th>
                      <th>{{ 'RawMaterial' | abpLocalization }}</th>
                      <th>{{ 'Uom' | abpLocalization }}</th>
                      <th class="text-end">{{ 'RequiredQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'ProcessLossQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'ReceivedQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'ReturnedQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'PendingQty' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of rep.rows ?? []; track $index) {
                      <tr>
                        <td>
                          <a [routerLink]="['/purchasing/subcontracting-inward', row.subcontractingInwardOrderId]">
                            {{ row.orderNumber }}
                          </a>
                        </td>
                        <td>{{ row.orderDate | date:'dd/MM/yyyy' }}</td>
                        <td>{{ row.partyName }}</td>
                        <td>
                          <div><code>{{ row.finishedGoodItemCode }}</code></div>
                          <small class="text-muted">{{ row.finishedGoodItemName }}</small>
                        </td>
                        <td>
                          <div><code>{{ row.rawMaterialItemCode }}</code></div>
                          <small class="text-muted">{{ row.rawMaterialItemName }}</small>
                        </td>
                        <td>{{ row.stockUom }}</td>
                        <td class="text-end">{{ row.requiredQty | number:'1.2-2' }}</td>
                        <td class="text-end text-danger">{{ row.processLossQty | number:'1.2-2' }}</td>
                        <td class="text-end text-success">{{ row.receivedQty | number:'1.2-2' }}</td>
                        <td class="text-end text-secondary">{{ row.returnedQty | number:'1.2-2' }}</td>
                        <td class="text-end text-warning fw-semibold">{{ row.pendingQty | number:'1.2-2' }}</td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="11" class="text-center py-4 text-muted">
                          {{ 'NoDataAvailable' | abpLocalization }}
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        }

        <!-- TAB 3: Order Summary -->
        @if (activeTab() === 'summary') {
          @if (summaryReport(); as rep) {
            <!-- KPI Cards -->
            <div class="row mb-3 g-2">
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-4 fw-bold text-primary">{{ rep.totalOrderQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'OrderQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-4 fw-bold text-success">{{ rep.totalDeliveredQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'DeliveredQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-4 fw-bold text-warning">{{ rep.totalPendingQty | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'PendingQty' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
              <div class="col-sm-3">
                <div class="card text-center border-0 shadow-sm">
                  <div class="card-body py-2">
                    <div class="fs-4 fw-bold text-secondary">{{ rep.totalAmount | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ 'TotalAmount' | abpLocalization }}</div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Table -->
            <div class="card shadow-sm">
              <div class="table-responsive">
                <table class="table table-hover table-sm align-middle mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ 'OrderNumber' | abpLocalization }}</th>
                      <th>{{ 'Date' | abpLocalization }}</th>
                      <th>{{ 'Supplier' | abpLocalization }}</th>
                      <th>{{ 'Status' | abpLocalization }}</th>
                      <th>{{ 'ItemCode' | abpLocalization }}</th>
                      <th>{{ 'ItemName' | abpLocalization }}</th>
                      <th>{{ 'Uom' | abpLocalization }}</th>
                      <th class="text-end">{{ 'OrderQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'DeliveredQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'PendingQty' | abpLocalization }}</th>
                      <th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                      <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of rep.rows ?? []; track $index) {
                      <tr>
                        <td>
                          <a [routerLink]="['/purchasing/subcontracting-inward', row.subcontractingInwardOrderId]">
                            {{ row.orderNumber }}
                          </a>
                        </td>
                        <td>{{ row.orderDate | date:'dd/MM/yyyy' }}</td>
                        <td>{{ row.partyName }}</td>
                        <td><span class="badge bg-secondary">{{ getStatusLabel(row.status ?? 0) }}</span></td>
                        <td><code>{{ row.itemCode }}</code></td>
                        <td>{{ row.itemName }}</td>
                        <td>{{ row.uom }}</td>
                        <td class="text-end">{{ row.orderQty | number:'1.2-2' }}</td>
                        <td class="text-end text-success">{{ row.deliveredQty | number:'1.2-2' }}</td>
                        <td class="text-end text-warning fw-semibold">{{ row.pendingQty | number:'1.2-2' }}</td>
                        <td class="text-end">{{ row.rate | number:'1.2-2' }}</td>
                        <td class="text-end fw-bold">{{ row.amount | number:'1.2-2' }}</td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="12" class="text-center py-4 text-muted">
                          {{ 'NoDataAvailable' | abpLocalization }}
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        }
      }
    </abp-page>
  `,
})
export class SubcontractingInwardReportsComponent implements OnInit {
  private reportService = inject(SubcontractingInwardReportService);
  private supplierService = inject(SupplierService);
  private companyContext = inject(CompanyContextService);

  activeTab = signal<ReportTab>('items');
  loading = signal<boolean>(false);
  suppliers = signal<SupplierDto[]>([]);

  itemsReport = signal<SubcontractedItemsToBeDeliveredReportDto | null>(null);
  materialsReport = signal<SubcontractedRawMaterialsToBeReceivedReportDto | null>(null);
  summaryReport = signal<SubcontractingInwardOrderSummaryReportDto | null>(null);

  fromDate = '';
  toDate = '';
  selectedSupplierId = '';
  selectedStatus = '';

  ngOnInit(): void {
    const today = new Date();
    const thirtyDaysAgo = new Date();
    thirtyDaysAgo.setDate(today.getDate() - 30);

    this.fromDate = thirtyDaysAgo.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];

    this.loadSuppliers();
    this.loadActiveReport();
  }

  loadSuppliers(): void {
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: res => this.suppliers.set(res.items ?? []),
      error: () => {},
    });
  }

  switchTab(tab: ReportTab): void {
    this.activeTab.set(tab);
    this.loadActiveReport();
  }

  private buildFilter(): SubcontractingInwardReportFilterDto {
    return {
      companyId: this.companyContext.selectedCompanyId() ?? undefined,
      fromDate: this.fromDate ? this.fromDate : undefined,
      toDate: this.toDate ? this.toDate : undefined,
      supplierId: this.selectedSupplierId ? this.selectedSupplierId : undefined,
      status: this.selectedStatus ? this.selectedStatus : undefined,
    };
  }

  loadActiveReport(): void {
    const filter = this.buildFilter();
    this.loading.set(true);

    if (this.activeTab() === 'items') {
      this.reportService.getItemsToBeDeliveredReport(filter).subscribe({
        next: res => {
          this.itemsReport.set(res);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    } else if (this.activeTab() === 'materials') {
      this.reportService.getRawMaterialsToBeReceivedReport(filter).subscribe({
        next: res => {
          this.materialsReport.set(res);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    } else if (this.activeTab() === 'summary') {
      this.reportService.getOrderSummaryReport(filter).subscribe({
        next: res => {
          this.summaryReport.set(res);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  getStatusLabel(status: number): string {
    const statusMap: Record<number, string> = {
      0: 'Draft',
      1: 'Open',
      2: 'Partially Received',
      3: 'Completed',
      4: 'Closed',
      5: 'Cancelled',
    };
    return statusMap[status] ?? 'Unknown';
  }

  exportCsv(): void {
    if (this.activeTab() === 'items') {
      const rows = this.itemsReport()?.rows ?? [];
      const mapped = rows.map(r => ({
        'Order Number': r.orderNumber ?? '',
        'Order Date': r.orderDate ? r.orderDate.split('T')[0] : '',
        'Supplier': r.partyName ?? '',
        'Item Code': r.itemCode ?? '',
        'Item Name': r.itemName ?? '',
        'UOM': r.uom ?? '',
        'Order Qty': r.orderQty ?? 0,
        'Produced Qty': r.producedQty ?? 0,
        'Delivered Qty': r.deliveredQty ?? 0,
        'Pending Qty': r.pendingQty ?? 0,
      }));
      exportToCsv('subcontracted-items-to-deliver.csv', mapped, [
        'Order Number', 'Order Date', 'Supplier', 'Item Code', 'Item Name', 'UOM',
        'Order Qty', 'Produced Qty', 'Delivered Qty', 'Pending Qty',
      ]);
    } else if (this.activeTab() === 'materials') {
      const rows = this.materialsReport()?.rows ?? [];
      const mapped = rows.map(r => ({
        'Order Number': r.orderNumber ?? '',
        'Order Date': r.orderDate ? r.orderDate.split('T')[0] : '',
        'Supplier': r.partyName ?? '',
        'FG Item Code': r.finishedGoodItemCode ?? '',
        'FG Item Name': r.finishedGoodItemName ?? '',
        'RM Item Code': r.rawMaterialItemCode ?? '',
        'RM Item Name': r.rawMaterialItemName ?? '',
        'Stock UOM': r.stockUom ?? '',
        'Required Qty': r.requiredQty ?? 0,
        'Process Loss Qty': r.processLossQty ?? 0,
        'Received Qty': r.receivedQty ?? 0,
        'Returned Qty': r.returnedQty ?? 0,
        'Pending Qty': r.pendingQty ?? 0,
      }));
      exportToCsv('subcontracted-raw-materials-to-receive.csv', mapped, [
        'Order Number', 'Order Date', 'Supplier', 'FG Item Code', 'FG Item Name',
        'RM Item Code', 'RM Item Name', 'Stock UOM', 'Required Qty', 'Process Loss Qty',
        'Received Qty', 'Returned Qty', 'Pending Qty',
      ]);
    } else if (this.activeTab() === 'summary') {
      const rows = this.summaryReport()?.rows ?? [];
      const mapped = rows.map(r => ({
        'Order Number': r.orderNumber ?? '',
        'Order Date': r.orderDate ? r.orderDate.split('T')[0] : '',
        'Supplier': r.partyName ?? '',
        'Status': this.getStatusLabel(r.status ?? 0),
        'Item Code': r.itemCode ?? '',
        'Item Name': r.itemName ?? '',
        'UOM': r.uom ?? '',
        'Order Qty': r.orderQty ?? 0,
        'Delivered Qty': r.deliveredQty ?? 0,
        'Pending Qty': r.pendingQty ?? 0,
        'Rate': r.rate ?? 0,
        'Amount': r.amount ?? 0,
      }));
      exportToCsv('subcontracting-inward-order-summary.csv', mapped, [
        'Order Number', 'Order Date', 'Supplier', 'Status', 'Item Code', 'Item Name',
        'UOM', 'Order Qty', 'Delivered Qty', 'Pending Qty', 'Rate', 'Amount',
      ]);
    }
  }
}
