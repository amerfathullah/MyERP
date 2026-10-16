import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PurchaseOrderAnalysisService } from '../../../proxy/purchasing/purchase-order-analysis.service';
import type {
  PurchaseOrderAnalysisReportDto,
  PurchaseOrderAnalysisRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-purchase-order-analysis',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-chart-bar text-primary me-2"></i>
            {{ '::PurchaseOrderAnalysis' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::PurchaseOrderAnalysisDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows && report()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::From' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::To' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::PurchaseOrder' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Search PO #"
                [(ngModel)]="poFilter"
                (keyup.enter)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <div class="form-check mt-3">
                <input
                  class="form-check-input"
                  type="checkbox"
                  id="groupByPo"
                  [(ngModel)]="groupByPo"
                  (change)="onGroupByPoChange()"
                />
                <label class="form-check-label small" for="groupByPo">
                  {{ '::GroupByPo' | abpLocalization }}
                </label>
              </div>
            </div>
            <div class="col-md-2">
              <div class="form-check mt-3">
                <input
                  class="form-check-input"
                  type="checkbox"
                  id="groupByItem"
                  [(ngModel)]="groupByItem"
                  (change)="onGroupByItemChange()"
                />
                <label class="form-check-label small" for="groupByItem">
                  {{ '::GroupByItem' | abpLocalization }}
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
      @if (report(); as rep) {
        <div class="row g-3 mb-3">
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::Quantity' | abpLocalization }}</div>
                <h4 class="fw-bold text-dark mb-0">{{ rep.totalQty | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::ReceivedQty' | abpLocalization }}</div>
                <h4 class="fw-bold text-success mb-0">{{ rep.totalReceivedQty | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::TotalAmount' | abpLocalization }}</div>
                <h4 class="fw-bold text-primary mb-0">{{ rep.totalAmount | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::BilledAmount' | abpLocalization }}</div>
                <h4 class="fw-bold text-info mb-0">{{ rep.totalBilledAmount | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::AmountToBill' | abpLocalization }}</div>
                <h4 class="fw-bold text-warning mb-0">{{ rep.totalAmountToBill | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
        </div>

        <!-- Table View -->
        <div class="card shadow-sm border-0">
          <div class="card-body p-0">
            @if (isLoading()) {
              <div class="text-center py-5">
                <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
              </div>
            } @else if (rep.rows.length === 0) {
              <div class="text-center py-5 text-muted">
                <i class="fa fa-info-circle fa-2x mb-2 d-block"></i>
                {{ '::NoPurchaseOrdersFound' | abpLocalization }}
              </div>
            } @else {
              <div class="table-responsive">
                <table class="table table-hover table-striped align-middle mb-0 small">
                  <thead class="table-light">
                    <tr>
                      @if (!groupByItem) {
                        <th>{{ '::PurchaseOrder' | abpLocalization }}</th>
                        <th>{{ '::Date' | abpLocalization }}</th>
                        <th>{{ '::RequiredDate' | abpLocalization }}</th>
                        <th>{{ '::Supplier' | abpLocalization }}</th>
                        <th>{{ '::Status' | abpLocalization }}</th>
                      }
                      @if (!groupByPo) {
                        <th>{{ '::ItemCode' | abpLocalization }}</th>
                        <th>{{ '::Description' | abpLocalization }}</th>
                        <th>{{ '::Uom' | abpLocalization }}</th>
                      }
                      <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                      <th class="text-end">{{ '::ReceivedQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::QtyToReceive' | abpLocalization }}</th>
                      <th class="text-end">{{ '::BilledQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::QtyToBill' | abpLocalization }}</th>
                      <th class="text-end">{{ '::TotalAmount' | abpLocalization }}</th>
                      <th class="text-end">{{ '::ReceivedAmount' | abpLocalization }}</th>
                      <th class="text-end">{{ '::BilledAmount' | abpLocalization }}</th>
                      <th class="text-end">{{ '::PendingAmount' | abpLocalization }}</th>
                      @if (!groupByPo && !groupByItem) {
                        <th class="text-center">{{ '::DelayDays' | abpLocalization }}</th>
                        <th>{{ '::Warehouse' | abpLocalization }}</th>
                      }
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of filteredRows(); track trackRow(row)) {
                      <tr>
                        @if (!groupByItem) {
                          <td class="fw-semibold text-primary">{{ row.purchaseOrderNumber || '-' }}</td>
                          <td>{{ row.date ? (row.date | date:'yyyy-MM-dd') : '-' }}</td>
                          <td>{{ row.requiredDate ? (row.requiredDate | date:'yyyy-MM-dd') : '-' }}</td>
                          <td>{{ row.supplierName || '-' }}</td>
                          <td>
                            <span class="badge" [ngClass]="getStatusBadgeClass(row.status)">
                              {{ row.status || '-' }}
                            </span>
                          </td>
                        }
                        @if (!groupByPo) {
                          <td class="fw-semibold">{{ row.itemCode || '-' }}</td>
                          <td>{{ row.description || '-' }}</td>
                          <td>{{ row.uom || '-' }}</td>
                        }
                        <td class="text-end">{{ row.qty | number:'1.2-2' }}</td>
                        <td class="text-end text-success">{{ row.receivedQty | number:'1.2-2' }}</td>
                        <td class="text-end">{{ row.pendingQty | number:'1.2-2' }}</td>
                        <td class="text-end text-info">{{ row.billedQty | number:'1.2-2' }}</td>
                        <td class="text-end">{{ row.qtyToBill | number:'1.2-2' }}</td>
                        <td class="text-end fw-semibold">{{ row.amount | number:'1.2-2' }}</td>
                        <td class="text-end text-success">{{ row.receivedQtyAmount | number:'1.2-2' }}</td>
                        <td class="text-end text-info">{{ row.billedAmount | number:'1.2-2' }}</td>
                        <td class="text-end text-warning">{{ row.pendingAmount | number:'1.2-2' }}</td>
                        @if (!groupByPo && !groupByItem) {
                          <td class="text-center">
                            @if (row.delayDays > 0) {
                              <span class="badge bg-danger">{{ row.delayDays }}d</span>
                            } @else if (row.delayDays === 0 && row.pendingQty > 0) {
                              <span class="badge bg-warning text-dark">Due</span>
                            } @else {
                              <span class="badge bg-light text-muted">0d</span>
                            }
                          </td>
                          <td>{{ row.warehouseName || '-' }}</td>
                        }
                      </tr>
                    }
                  </tbody>
                  <tfoot class="table-light fw-bold">
                    <tr>
                      <td [attr.colspan]="getColspanLeading()">{{ '::Total' | abpLocalization }}</td>
                      <td class="text-end">{{ rep.totalQty | number:'1.2-2' }}</td>
                      <td class="text-end text-success">{{ rep.totalReceivedQty | number:'1.2-2' }}</td>
                      <td class="text-end"></td>
                      <td class="text-end"></td>
                      <td class="text-end"></td>
                      <td class="text-end">{{ rep.totalAmount | number:'1.2-2' }}</td>
                      <td class="text-end text-success"></td>
                      <td class="text-end text-info">{{ rep.totalBilledAmount | number:'1.2-2' }}</td>
                      <td class="text-end text-warning">{{ rep.totalAmountToBill | number:'1.2-2' }}</td>
                      @if (!groupByPo && !groupByItem) {
                        <td colspan="2"></td>
                      }
                    </tr>
                  </tfoot>
                </table>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class PurchaseOrderAnalysisComponent implements OnInit {
  private service = inject(PurchaseOrderAnalysisService);
  private companyContext = inject(CompanyContextService);

  report = signal<PurchaseOrderAnalysisReportDto | null>(null);
  isLoading = signal(false);

  fromDate = '';
  toDate = '';
  poFilter = '';
  groupByPo = false;
  groupByItem = false;

  ngOnInit(): void {
    const now = new Date();
    const startOfYear = new Date(now.getFullYear(), 0, 1);
    this.fromDate = startOfYear.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
    this.loadReport();
  }

  onGroupByPoChange(): void {
    if (this.groupByPo) {
      this.groupByItem = false;
    }
    this.loadReport();
  }

  onGroupByItemChange(): void {
    if (this.groupByItem) {
      this.groupByPo = false;
    }
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getAnalysis({
        companyId,
        fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : null,
        toDate: this.toDate ? new Date(this.toDate).toISOString() : null,
        groupByPo: this.groupByPo,
        groupByItem: this.groupByItem,
      })
      .subscribe({
        next: (res) => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  filteredRows(): PurchaseOrderAnalysisRowDto[] {
    const rep = this.report();
    if (!rep) return [];
    if (!this.poFilter.trim()) return rep.rows;

    const term = this.poFilter.trim().toLowerCase();
    return rep.rows.filter(
      (r) =>
        r.purchaseOrderNumber?.toLowerCase().includes(term) ||
        r.supplierName?.toLowerCase().includes(term) ||
        r.itemCode?.toLowerCase().includes(term)
    );
  }

  trackRow(row: PurchaseOrderAnalysisRowDto): string {
    return `${row.purchaseOrderId || ''}_${row.itemId || ''}_${row.itemCode || ''}_${row.purchaseOrderNumber || ''}`;
  }

  getColspanLeading(): number {
    let count = 0;
    if (!this.groupByItem) count += 5;
    if (!this.groupByPo) count += 3;
    return count;
  }

  getStatusBadgeClass(status?: string | null): string {
    switch (status) {
      case 'Submitted':
      case 'ToDeliverAndBill':
        return 'bg-info text-dark';
      case 'Completed':
        return 'bg-success';
      case 'Closed':
        return 'bg-secondary';
      case 'Cancelled':
        return 'bg-danger';
      default:
        return 'bg-light text-dark';
    }
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows.length) return;

    const exportRows = rep.rows.map((r) => ({
      purchaseOrderNumber: r.purchaseOrderNumber || '',
      date: r.date || '',
      requiredDate: r.requiredDate || '',
      supplierName: r.supplierName || '',
      status: r.status || '',
      itemCode: r.itemCode || '',
      description: r.description || '',
      uom: r.uom || '',
      qty: r.qty,
      receivedQty: r.receivedQty,
      pendingQty: r.pendingQty,
      billedQty: r.billedQty,
      qtyToBill: r.qtyToBill,
      amount: r.amount,
      receivedQtyAmount: r.receivedQtyAmount,
      billedAmount: r.billedAmount,
      pendingAmount: r.pendingAmount,
      delayDays: r.delayDays,
      warehouseName: r.warehouseName || '',
    }));

    exportToCsv(
      `Purchase_Order_Analysis_${new Date().toISOString().split('T')[0]}.csv`,
      exportRows,
      [
        'purchaseOrderNumber',
        'date',
        'requiredDate',
        'supplierName',
        'status',
        'itemCode',
        'description',
        'uom',
        'qty',
        'receivedQty',
        'pendingQty',
        'billedQty',
        'qtyToBill',
        'amount',
        'receivedQtyAmount',
        'billedAmount',
        'pendingAmount',
        'delayDays',
        'warehouseName',
      ]
    );
  }
}
