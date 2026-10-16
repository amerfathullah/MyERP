import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SubcontractOrderSummaryService } from '../../../proxy/purchasing/subcontract-order-summary.service';
import type {
  SubcontractOrderSummaryReportDto,
  SubcontractOrderSummaryRowDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-subcontract-order-summary',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-clipboard-list text-primary me-2"></i>
            {{ '::SubcontractOrderSummary' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SubcontractOrderSummaryDescription' | abpLocalization }}
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
              <label class="form-label small mb-1">{{ '::SubcontractingOrder' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Search Subcontract Order / Supplier / Item"
                [(ngModel)]="filterText"
              />
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
          <div class="col-md-2">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::SubcontractingOrder' | abpLocalization }}s</div>
                <h4 class="fw-bold text-dark mb-0">{{ rep.totalOrders }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-2">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">FG Ordered</div>
                <h4 class="fw-bold text-primary mb-0">{{ rep.totalOrderQty | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-2">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">FG Received</div>
                <h4 class="fw-bold text-success mb-0">{{ rep.totalReceivedQty | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-2">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">RM Required</div>
                <h4 class="fw-bold text-dark mb-0">{{ rep.totalRequiredQty | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-2">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">RM Supplied</div>
                <h4 class="fw-bold text-info mb-0">{{ rep.totalSuppliedQty | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-2">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">RM Consumed</div>
                <h4 class="fw-bold text-warning mb-0">{{ rep.totalConsumedQty | number:'1.2-2' }}</h4>
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
            } @else if (!filteredRows().length) {
              <div class="text-center py-5 text-muted">
                <i class="fa fa-info-circle fa-2x mb-2 d-block"></i>
                {{ '::NoSubcontractOrdersFound' | abpLocalization }}
              </div>
            } @else {
              <div class="table-responsive">
                <table class="table table-hover table-striped align-middle mb-0 small">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::SubcontractingOrder' | abpLocalization }}</th>
                      <th>{{ '::Date' | abpLocalization }}</th>
                      <th>{{ '::Supplier' | abpLocalization }}</th>
                      <th>{{ '::Status' | abpLocalization }}</th>
                      <th>{{ '::SubcontractedItem' | abpLocalization }}</th>
                      <th class="text-end">Order Qty</th>
                      <th class="text-end">{{ '::ReceivedQty' | abpLocalization }}</th>
                      <th class="text-end">Pending FG</th>
                      <th>{{ '::SuppliedItem' | abpLocalization }}</th>
                      <th class="text-end">{{ '::RequiredQuantity' | abpLocalization }}</th>
                      <th class="text-end">{{ '::TransferredQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::ConsumedQty' | abpLocalization }}</th>
                      <th class="text-end">Pending RM</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of filteredRows(); track trackRow(row)) {
                      <tr>
                        <td class="fw-semibold text-primary">{{ row.subcontractOrderNumber }}</td>
                        <td>{{ row.orderDate | date:'yyyy-MM-dd' }}</td>
                        <td>{{ row.supplierName }}</td>
                        <td>
                          <span class="badge" [ngClass]="getStatusBadgeClass(row.status)">
                            {{ row.status }}
                          </span>
                        </td>
                        <td class="fw-semibold text-dark">{{ row.fgItemCode || '-' }}</td>
                        <td class="text-end">{{ row.orderQty | number:'1.2-2' }}</td>
                        <td class="text-end text-success">{{ row.receivedQty | number:'1.2-2' }}</td>
                        <td class="text-end">
                          @if (row.pendingFgQty > 0) {
                            <span class="badge bg-warning text-dark">{{ row.pendingFgQty | number:'1.2-2' }}</span>
                          } @else {
                            <span class="text-muted">0.00</span>
                          }
                        </td>
                        <td class="fw-semibold">{{ row.rmItemCode || '-' }}</td>
                        <td class="text-end">{{ row.requiredQty | number:'1.2-2' }}</td>
                        <td class="text-end text-info">{{ row.suppliedQty | number:'1.2-2' }}</td>
                        <td class="text-end text-secondary">{{ row.consumedQty | number:'1.2-2' }}</td>
                        <td class="text-end">
                          @if (row.pendingRmQty > 0) {
                            <span class="badge bg-danger">{{ row.pendingRmQty | number:'1.2-2' }}</span>
                          } @else {
                            <span class="text-muted">0.00</span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                  <tfoot class="table-light fw-bold">
                    <tr>
                      <td colspan="5">{{ '::Total' | abpLocalization }}</td>
                      <td class="text-end text-primary">{{ rep.totalOrderQty | number:'1.2-2' }}</td>
                      <td class="text-end text-success">{{ rep.totalReceivedQty | number:'1.2-2' }}</td>
                      <td></td>
                      <td></td>
                      <td class="text-end">{{ rep.totalRequiredQty | number:'1.2-2' }}</td>
                      <td class="text-end text-info">{{ rep.totalSuppliedQty | number:'1.2-2' }}</td>
                      <td class="text-end text-secondary">{{ rep.totalConsumedQty | number:'1.2-2' }}</td>
                      <td></td>
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
export class SubcontractOrderSummaryComponent implements OnInit {
  private service = inject(SubcontractOrderSummaryService);
  private companyContext = inject(CompanyContextService);

  report = signal<SubcontractOrderSummaryReportDto | null>(null);
  isLoading = signal(false);

  fromDate = '';
  toDate = '';
  filterText = '';

  ngOnInit(): void {
    const now = new Date();
    const startOfYear = new Date(now.getFullYear(), 0, 1);
    this.fromDate = startOfYear.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
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
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  filteredRows(): SubcontractOrderSummaryRowDto[] {
    const rep = this.report();
    if (!rep || !rep.rows) return [];
    if (!this.filterText.trim()) return rep.rows;

    const term = this.filterText.trim().toLowerCase();
    return rep.rows.filter(
      (r) =>
        r.subcontractOrderNumber.toLowerCase().includes(term) ||
        r.supplierName.toLowerCase().includes(term) ||
        (r.fgItemCode && r.fgItemCode.toLowerCase().includes(term)) ||
        (r.rmItemCode && r.rmItemCode.toLowerCase().includes(term))
    );
  }

  trackRow(row: SubcontractOrderSummaryRowDto): string {
    return `${row.subcontractOrderId}_${row.fgItemId || ''}_${row.rmItemId || ''}`;
  }

  getStatusBadgeClass(status?: string | null): string {
    switch (status) {
      case 'Open':
        return 'bg-info text-dark';
      case 'PartiallyReceived':
        return 'bg-warning text-dark';
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
    if (!rep || !rep.rows || !rep.rows.length) return;

    const exportRows = rep.rows.map((r) => ({
      subcontractOrderNumber: r.subcontractOrderNumber,
      orderDate: r.orderDate,
      supplierName: r.supplierName,
      status: r.status,
      fgItemCode: r.fgItemCode || '',
      fgItemName: r.fgItemName || '',
      orderQty: r.orderQty,
      receivedQty: r.receivedQty,
      pendingFgQty: r.pendingFgQty,
      rmItemCode: r.rmItemCode || '',
      rmItemName: r.rmItemName || '',
      requiredQty: r.requiredQty,
      suppliedQty: r.suppliedQty,
      consumedQty: r.consumedQty,
      returnedQty: r.returnedQty,
      pendingRmQty: r.pendingRmQty,
    }));

    exportToCsv(
      `Subcontract_Order_Summary_${new Date().toISOString().split('T')[0]}.csv`,
      exportRows,
      [
        'subcontractOrderNumber',
        'orderDate',
        'supplierName',
        'status',
        'fgItemCode',
        'fgItemName',
        'orderQty',
        'receivedQty',
        'pendingFgQty',
        'rmItemCode',
        'rmItemName',
        'requiredQty',
        'suppliedQty',
        'consumedQty',
        'returnedQty',
        'pendingRmQty',
      ]
    );
  }
}
