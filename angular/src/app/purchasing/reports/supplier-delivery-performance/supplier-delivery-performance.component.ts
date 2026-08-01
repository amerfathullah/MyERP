import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { SupplierDeliveryPerformanceService } from '../../../proxy/purchasing/supplier-delivery-performance.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface SupplierPerformanceRow {
  supplierId: string;
  supplierName: string;
  totalOrders: number;
  onTimeDeliveries: number;
  lateDeliveries: number;
  pendingDeliveries: number;
  onTimeRate: number;
  avgDelayDays: number;
  totalOrderValue: number;
}

interface DeliveryPerformanceReport {
  suppliers: SupplierPerformanceRow[];
  totalOrders: number;
  totalOnTime: number;
  totalLate: number;
  totalPending: number;
  overallOnTimeRate: number;
  overallAvgDelayDays: number;
}

@Component({
  standalone: true,
  selector: 'app-supplier-delivery-performance',
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4><i class="fa fa-truck-clock me-2"></i>{{ '::SupplierDeliveryPerformance' | abpLocalization }}</h4>
        @if (report()) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small">{{ '::From' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadReport()">
            </div>
            <div class="col-md-3">
              <label class="form-label small">{{ '::To' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadReport()">
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button class="btn btn-primary btn-sm" (click)="loadReport()" [disabled]="isLoading()">
                <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (report(); as r) {
        <!-- KPI Cards -->
        <div class="row g-3 mb-4">
          <div class="col-md-2">
            <div class="border rounded p-3 text-center">
              <div class="fs-4 fw-bold text-primary">{{ r.totalOrders }}</div>
              <small class="text-muted">{{ '::TotalOrders' | abpLocalization }}</small>
            </div>
          </div>
          <div class="col-md-2">
            <div class="border rounded p-3 text-center border-success">
              <div class="fs-4 fw-bold text-success">{{ r.overallOnTimeRate }}%</div>
              <small class="text-muted">{{ '::OnTimeRate' | abpLocalization }}</small>
            </div>
          </div>
          <div class="col-md-2">
            <div class="border rounded p-3 text-center">
              <div class="fs-4 fw-bold text-success">{{ r.totalOnTime }}</div>
              <small class="text-muted">{{ '::OnTime' | abpLocalization }}</small>
            </div>
          </div>
          <div class="col-md-2">
            <div class="border rounded p-3 text-center" [class.border-danger]="r.totalLate > 0">
              <div class="fs-4 fw-bold text-danger">{{ r.totalLate }}</div>
              <small class="text-muted">{{ '::Late' | abpLocalization }}</small>
            </div>
          </div>
          <div class="col-md-2">
            <div class="border rounded p-3 text-center">
              <div class="fs-4 fw-bold text-warning">{{ r.totalPending }}</div>
              <small class="text-muted">{{ '::Pending' | abpLocalization }}</small>
            </div>
          </div>
          <div class="col-md-2">
            <div class="border rounded p-3 text-center" [class.border-danger]="r.overallAvgDelayDays > 5">
              <div class="fs-4 fw-bold" [class.text-danger]="r.overallAvgDelayDays > 5">{{ r.overallAvgDelayDays }}d</div>
              <small class="text-muted">{{ '::AvgDelay' | abpLocalization }}</small>
            </div>
          </div>
        </div>

        <!-- Performance Table -->
        <div class="card">
          <div class="card-header">
            <h6 class="mb-0">{{ '::BySupplier' | abpLocalization }} ({{ r.suppliers.length }})</h6>
          </div>
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover table-sm mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Supplier' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Orders' | abpLocalization }}</th>
                    <th class="text-center">{{ '::OnTime' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Late' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Pending' | abpLocalization }}</th>
                    <th class="text-center">{{ '::OnTimeRate' | abpLocalization }}</th>
                    <th class="text-center">{{ '::AvgDelay' | abpLocalization }}</th>
                    <th class="text-end">{{ '::OrderValue' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (s of r.suppliers; track s.supplierId) {
                    <tr [class.table-danger]="s.onTimeRate < 50" [class.table-warning]="s.onTimeRate >= 50 && s.onTimeRate < 80">
                      <td>
                        <a [routerLink]="['/suppliers', s.supplierId]" class="text-decoration-none">
                          {{ s.supplierName }}
                        </a>
                      </td>
                      <td class="text-center">{{ s.totalOrders }}</td>
                      <td class="text-center text-success fw-bold">{{ s.onTimeDeliveries }}</td>
                      <td class="text-center text-danger fw-bold">{{ s.lateDeliveries }}</td>
                      <td class="text-center text-muted">{{ s.pendingDeliveries }}</td>
                      <td class="text-center">
                        <span class="badge"
                          [class.bg-success]="s.onTimeRate >= 80"
                          [class.bg-warning]="s.onTimeRate >= 50 && s.onTimeRate < 80"
                          [class.bg-danger]="s.onTimeRate < 50">
                          {{ s.onTimeRate }}%
                        </span>
                      </td>
                      <td class="text-center" [class.text-danger]="s.avgDelayDays > 5">
                        {{ s.avgDelayDays > 0 ? s.avgDelayDays + 'd' : '—' }}
                      </td>
                      <td class="text-end">{{ s.totalOrderValue | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      } @else if (!isLoading()) {
        <div class="text-center py-5 text-muted">
          <i class="fa fa-truck fa-3x mb-3 opacity-25"></i>
          <p>{{ '::ClickGenerateToViewReport' | abpLocalization }}</p>
        </div>
      }

      @if (isLoading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
    </div>
  `
})
export class SupplierDeliveryPerformanceComponent implements OnInit {
  private reportService = inject(SupplierDeliveryPerformanceService);
  private companyContext = inject(CompanyContextService);
  private l = inject(LocalizationService);

  report = signal<DeliveryPerformanceReport | null>(null);
  isLoading = signal(false);

  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    const now = new Date();
    const sixMonthsAgo = new Date(now.getFullYear(), now.getMonth() - 6, 1);
    this.fromDate = sixMonthsAgo.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.reportService.getReport({ companyId, fromDate: this.fromDate, toDate: this.toDate } as any).subscribe({
      next: (data) => {
        this.report.set(data as any);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;
    exportToCsv('supplier-delivery-performance.csv', r.suppliers, [
      'supplierName', 'totalOrders', 'onTimeDeliveries', 'lateDeliveries',
      'pendingDeliveries', 'onTimeRate', 'avgDelayDays', 'totalOrderValue'
    ]);
  }
}
