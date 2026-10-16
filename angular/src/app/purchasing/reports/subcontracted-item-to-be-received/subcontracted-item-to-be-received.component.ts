import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SubcontractedItemToBeReceivedService } from '../../../proxy/purchasing/subcontracted-item-to-be-received.service';
import { SupplierService } from '../../../proxy/purchasing/supplier.service';
import {
  SubcontractedItemToBeReceivedFilterDto,
  SubcontractedItemToBeReceivedReportDto,
  SubcontractedItemToBeReceivedRowDto,
  SupplierDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  selector: 'app-subcontracted-item-to-be-received',
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-boxes-packing text-primary me-2"></i>
            {{ '::SubcontractedItemToBeReceived' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SubcontractedItemToBeReceivedDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows && report()!.rows!.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filter Card -->
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
              <label class="form-label small mb-1">{{ '::Supplier' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedSupplierId"
                (change)="loadReport()"
              >
                <option [ngValue]="null">{{ '::AllSuppliers' | abpLocalization }}</option>
                @for (sup of suppliers(); track sup.id) {
                  <option [ngValue]="sup.id">{{ sup.name }}</option>
                }
              </select>
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
          <div class="col-md-3">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::TotalOrders' | abpLocalization }}</div>
                <h4 class="fw-bold text-dark mb-0">{{ s.totalOrders || 0 }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::RequiredQty' | abpLocalization }}</div>
                <h4 class="fw-bold text-primary mb-0">{{ (s.totalRequiredQty || 0) | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::ReceivedQty' | abpLocalization }}</div>
                <h4 class="fw-bold text-success mb-0">{{ (s.totalReceivedQty || 0) | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card shadow-sm border-0 h-100">
              <div class="card-body py-3 text-center">
                <div class="text-muted small mb-1">{{ '::PendingQuantity' | abpLocalization }}</div>
                <h4 class="fw-bold text-danger mb-0">{{ (s.totalPendingQty || 0) | number:'1.2-2' }}</h4>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Report Table Card -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (isLoading()) {
            <div class="text-center py-5">
              <i class="fa fa-spinner fa-spin fa-2x text-primary mb-2"></i>
              <div class="text-muted small">{{ '::Loading' | abpLocalization }}</div>
            </div>
          } @else if (report()?.rows && report()!.rows!.length > 0) {
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::SubcontractOrder' | abpLocalization }}</th>
                    <th>{{ '::Date' | abpLocalization }}</th>
                    <th>{{ '::Supplier' | abpLocalization }}</th>
                    <th>{{ '::FgItemCode' | abpLocalization }}</th>
                    <th>{{ '::ItemName' | abpLocalization }}</th>
                    <th class="text-end">{{ '::RequiredQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReceivedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::PendingQuantity' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Progress' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows!; track row.subcontractOrderId + '-' + row.fgItemId) {
                    <tr>
                      <td class="fw-medium text-primary">{{ row.subcontractOrderNumber }}</td>
                      <td>{{ row.orderDate | date:'mediumDate' }}</td>
                      <td>{{ row.supplierName }}</td>
                      <td><code>{{ row.fgItemCode }}</code></td>
                      <td>{{ row.itemName }}</td>
                      <td class="text-end">{{ row.requiredQty | number:'1.2-2' }}</td>
                      <td class="text-end text-success">{{ row.receivedQty | number:'1.2-2' }}</td>
                      <td class="text-end text-danger fw-bold">{{ row.pendingQty | number:'1.2-2' }}</td>
                      <td class="text-center" style="width: 140px;">
                        @let pct = getProgressPercent(row);
                        <div class="progress" style="height: 16px;">
                          <div
                            class="progress-bar progress-bar-striped"
                            [class.bg-success]="pct === 100"
                            [class.bg-warning]="pct < 100 && pct > 0"
                            [class.bg-danger]="pct === 0"
                            role="progressbar"
                            [style.width.%]="pct"
                            [attr.aria-valuenow]="pct"
                            aria-valuemin="0"
                            aria-valuemax="100"
                          >
                            <small style="font-size: 10px;">{{ pct }}%</small>
                          </div>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-box-open fa-3x mb-3 text-secondary opacity-50"></i>
              <p class="mb-0">{{ '::NoSubcontractedItemsToBeReceived' | abpLocalization }}</p>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class SubcontractedItemToBeReceivedComponent implements OnInit {
  private service = inject(SubcontractedItemToBeReceivedService);
  private supplierService = inject(SupplierService);
  private companyContext = inject(CompanyContextService);

  report = signal<SubcontractedItemToBeReceivedReportDto | null>(null);
  suppliers = signal<SupplierDto[]>([]);
  isLoading = signal<boolean>(false);

  fromDate: string = '';
  toDate: string = '';
  selectedSupplierId: string | null = null;

  ngOnInit(): void {
    const today = new Date();
    const oneMonthAgo = new Date();
    oneMonthAgo.setMonth(today.getMonth() - 1);

    this.fromDate = oneMonthAgo.toISOString().substring(0, 10);
    this.toDate = today.toISOString().substring(0, 10);

    this.loadSuppliers();
    this.loadReport();
  }

  loadSuppliers(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.supplierService
      .getList({ maxResultCount: 500 })
      .subscribe({
        next: res => {
          this.suppliers.set(res.items || []);
        },
      });
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    const filter: SubcontractedItemToBeReceivedFilterDto = {
      companyId,
      supplierId: this.selectedSupplierId || undefined,
      fromDate: this.fromDate ? new Date(this.fromDate).toISOString() : undefined,
      toDate: this.toDate ? new Date(this.toDate).toISOString() : undefined,
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

  getProgressPercent(row: SubcontractedItemToBeReceivedRowDto): number {
    if (!row.requiredQty || row.requiredQty <= 0) return 0;
    const pct = Math.round(((row.receivedQty || 0) / row.requiredQty) * 100);
    return Math.min(100, Math.max(0, pct));
  }

  exportCsv(): void {
    const data = this.report()?.rows;
    if (!data || data.length === 0) return;

    const exportRows = data.map(r => ({
      'Subcontract Order': r.subcontractOrderNumber,
      Date: r.orderDate ? r.orderDate.substring(0, 10) : '',
      Supplier: r.supplierName,
      'FG Item Code': r.fgItemCode,
      'Item Name': r.itemName,
      'Required Quantity': r.requiredQty,
      'Received Quantity': r.receivedQty,
      'Pending Quantity': r.pendingQty,
    }));

    const columns = [
      'Subcontract Order',
      'Date',
      'Supplier',
      'FG Item Code',
      'Item Name',
      'Required Quantity',
      'Received Quantity',
      'Pending Quantity',
    ];

    exportToCsv(
      `subcontracted-items-to-be-received-${new Date().toISOString().substring(0, 10)}.csv`,
      exportRows,
      columns
    );
  }
}
