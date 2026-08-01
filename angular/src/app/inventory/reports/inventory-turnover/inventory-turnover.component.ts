import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { InventoryTurnoverService } from '../../../proxy/inventory/inventory-turnover.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface TurnoverItem {
  itemId: string;
  itemCode: string;
  itemName: string;
  consumedQty: number;
  consumedValue: number;
  currentStockQty: number;
  currentStockValue: number;
  turnoverRatio: number;
  daysToSell: number;
  category: string;
}

interface TurnoverReport {
  fromDate: string;
  toDate: string;
  periodDays: number;
  totalItems: number;
  fastMovingCount: number;
  slowMovingCount: number;
  deadStockCount: number;
  totalStockValue: number;
  totalConsumedValue: number;
  items: TurnoverItem[];
}

@Component({
  selector: 'app-inventory-turnover',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fa fa-chart-bar me-2"></i>{{ '::InventoryTurnoverAnalysis' | abpLocalization }}</h5>
        @if (report()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row mb-3 g-2">
          <div class="col-md-3">
            <label class="form-label small">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label small">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-3 d-flex align-items-end">
            <button class="btn btn-primary btn-sm" (click)="loadReport()" [disabled]="loading()">
              @if (loading()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              {{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (report(); as r) {
          <!-- KPI Summary Cards -->
          <div class="row mb-4 g-3">
            <div class="col-md-3">
              <div class="card border-start border-4 border-primary">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalItems' | abpLocalization }}</div>
                  <div class="fw-bold fs-5">{{ r.totalItems }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-start border-4 border-success">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::FastMoving' | abpLocalization }}</div>
                  <div class="fw-bold fs-5 text-success">{{ r.fastMovingCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-start border-4 border-warning">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::SlowMoving' | abpLocalization }}</div>
                  <div class="fw-bold fs-5 text-warning">{{ r.slowMovingCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-start border-4 border-danger">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::DeadStock' | abpLocalization }}</div>
                  <div class="fw-bold fs-5 text-danger">{{ r.deadStockCount }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Category Distribution Bar -->
          <div class="mb-4">
            <div class="d-flex" style="height: 8px; border-radius: 4px; overflow: hidden;">
              <div class="bg-success" [style.width.%]="(r.fastMovingCount / r.totalItems) * 100"></div>
              <div class="bg-info" [style.width.%]="((r.totalItems - r.fastMovingCount - r.slowMovingCount - r.deadStockCount) / r.totalItems) * 100"></div>
              <div class="bg-warning" [style.width.%]="(r.slowMovingCount / r.totalItems) * 100"></div>
              <div class="bg-danger" [style.width.%]="(r.deadStockCount / r.totalItems) * 100"></div>
            </div>
          </div>

          <!-- Items Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ConsumedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ConsumedValue' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CurrentStock' | abpLocalization }}</th>
                  <th class="text-end">{{ '::StockValue' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TurnoverRatio' | abpLocalization }}</th>
                  <th class="text-end">{{ '::DaysToSell' | abpLocalization }}</th>
                  <th class="text-center">{{ '::Category' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of r.items; track item.itemId) {
                  <tr [class.table-danger]="item.category === 'Dead Stock'"
                      [class.table-warning]="item.category === 'Slow Moving'">
                    <td>
                      <div class="fw-medium">{{ item.itemName || item.itemCode }}</div>
                      <small class="text-muted">{{ item.itemCode }}</small>
                    </td>
                    <td class="text-end">{{ item.consumedQty | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.consumedValue | number:'1.2-2' }}</td>
                    <td class="text-end">{{ item.currentStockQty | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.currentStockValue | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold">{{ item.turnoverRatio | number:'1.2-2' }}x</td>
                    <td class="text-end">
                      @if (item.daysToSell > 0) { {{ item.daysToSell | number:'1.0-0' }}d }
                      @else { <span class="text-muted">—</span> }
                    </td>
                    <td class="text-center">
                      <span class="badge" [class.bg-success]="item.category === 'Fast Moving'"
                            [class.bg-info]="item.category === 'Normal'"
                            [class.bg-warning]="item.category === 'Slow Moving'"
                            [class.bg-danger]="item.category === 'Dead Stock'">
                        {{ item.category }}
                      </span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else if (!loading()) {
          <div class="text-center text-muted py-5">
            <i class="fa fa-chart-bar fa-3x mb-3 opacity-25"></i>
            <p>{{ '::ClickGenerateToViewReport' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `,
})
export class InventoryTurnoverComponent implements OnInit {
  private turnoverService = inject(InventoryTurnoverService);
  private companyContext = inject(CompanyContextService);

  report = signal<TurnoverReport | null>(null);
  loading = signal(false);

  fromDate = '';
  toDate = '';

  ngOnInit(): void {
    const now = new Date();
    const threeMonthsAgo = new Date(now.getFullYear(), now.getMonth() - 3, 1);
    this.fromDate = threeMonthsAgo.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.loading.set(true);
    this.turnoverService
      .getReport(companyId, this.fromDate, this.toDate)
      .subscribe({
        next: data => {
          this.report.set(data as any);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;
    const rows = r.items.map(i => ({
      'Item Code': i.itemCode,
      'Item Name': i.itemName,
      'Consumed Qty': i.consumedQty,
      'Consumed Value': i.consumedValue,
      'Current Stock': i.currentStockQty,
      'Stock Value': i.currentStockValue,
      'Turnover Ratio': i.turnoverRatio,
      'Days to Sell': i.daysToSell,
      'Category': i.category,
    }));
    exportToCsv(
      `inventory-turnover-${this.fromDate}-to-${this.toDate}.csv`,
      rows,
      ['Item Code', 'Item Name', 'Consumed Qty', 'Consumed Value', 'Current Stock', 'Stock Value', 'Turnover Ratio', 'Days to Sell', 'Category']
    );
  }
}
