import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface StockGlComparisonDto {
  totalStockValue: number;
  totalGlBalance: number;
  difference: number;
  isMatched: boolean;
  warehouseCount: number;
  itemCount: number;
  asOfDate: string;
  perWarehouse: WarehouseComparisonDto[];
}

interface WarehouseComparisonDto {
  warehouseId: string;
  warehouseName: string;
  stockValue: number;
  glBalance: number;
  difference: number;
  hasMismatch: boolean;
  stockAccountId?: string;
  stockAccountName?: string;
}

@Component({
  selector: 'app-stock-gl-comparison',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe, CompanyCurrencyPipe],
  template: `
    <abp-page [title]="'::StockGlComparison' | abpLocalization">
      <div class="card mb-3">
        <div class="card-body">
          <div class="row align-items-end g-3">
            <div class="col-md-4">
              <label class="form-label">{{ '::Company' | abpLocalization }}</label>
              <select class="form-select" [formControl]="companyId">
                @for (c of companies(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::AsOfDate' | abpLocalization }}</label>
              <input type="date" class="form-control" [formControl]="asOfDate" />
            </div>
            <div class="col-md-3">
              <button class="btn btn-primary" (click)="runComparison()" [disabled]="isLoading()">
                @if (isLoading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                }
                <i class="fa fa-balance-scale me-1"></i>{{ '::Compare' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (result()) {
        <!-- Summary KPI Cards -->
        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <div class="card border-primary">
              <div class="card-body text-center">
                <div class="text-muted small">{{ '::StockValue' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-primary">{{ "" | companyCurrency }} {{ result()!.totalStockValue | number:'1.2-2' }}</div>
                <div class="text-muted small">{{ result()!.itemCount }} {{ '::Items' | abpLocalization }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-info">
              <div class="card-body text-center">
                <div class="text-muted small">{{ '::GLBalance' | abpLocalization }}</div>
                <div class="fs-4 fw-bold text-info">{{ "" | companyCurrency }} {{ result()!.totalGlBalance | number:'1.2-2' }}</div>
                <div class="text-muted small">{{ '::StockAccounts' | abpLocalization }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card" [class.border-success]="result()!.isMatched" [class.border-danger]="!result()!.isMatched">
              <div class="card-body text-center">
                <div class="text-muted small">{{ '::Difference' | abpLocalization }}</div>
                <div class="fs-4 fw-bold" [class.text-success]="result()!.isMatched" [class.text-danger]="!result()!.isMatched">
                  {{ "" | companyCurrency }} {{ result()!.difference | number:'1.2-2' }}
                </div>
                <div class="small">
                  @if (result()!.isMatched) {
                    <span class="badge bg-success"><i class="fa fa-check-circle me-1"></i>{{ '::Matched' | abpLocalization }}</span>
                  } @else {
                    <span class="badge bg-danger"><i class="fa fa-exclamation-triangle me-1"></i>{{ '::Mismatch' | abpLocalization }}</span>
                  }
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ '::Warehouses' | abpLocalization }}</div>
                <div class="fs-4 fw-bold">{{ result()!.warehouseCount }}</div>
                <div class="text-muted small">
                  {{ getMismatchCount() }} {{ '::WithMismatch' | abpLocalization }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Per-Warehouse Comparison Table -->
        @if (result()!.perWarehouse.length > 0) {
          <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
              <span class="fw-bold">
                <i class="fa fa-warehouse me-2"></i>{{ '::PerWarehouseComparison' | abpLocalization }}
              </span>
              <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
                <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
              </button>
            </div>
            <div class="card-body p-0">
              <div class="table-responsive">
                <table class="table table-hover mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::Warehouse' | abpLocalization }}</th>
                      <th>{{ '::StockAccount' | abpLocalization }}</th>
                      <th class="text-end">{{ '::StockValue' | abpLocalization }}</th>
                      <th class="text-end">{{ '::GLBalance' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Difference' | abpLocalization }}</th>
                      <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (wh of result()!.perWarehouse; track wh.warehouseId) {
                      <tr [class.table-danger]="wh.hasMismatch">
                        <td class="fw-bold">{{ wh.warehouseName }}</td>
                        <td class="text-muted small">{{ wh.stockAccountName || '—' }}</td>
                        <td class="text-end font-monospace">{{ wh.stockValue | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace">{{ wh.glBalance | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace" [class.text-danger]="wh.hasMismatch" [class.fw-bold]="wh.hasMismatch">
                          {{ wh.difference | number:'1.2-2' }}
                        </td>
                        <td class="text-center">
                          @if (wh.hasMismatch) {
                            <span class="badge bg-danger">{{ '::Mismatch' | abpLocalization }}</span>
                          } @else {
                            <span class="badge bg-success"><i class="fa fa-check"></i></span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                  <tfoot class="table-light fw-bold">
                    <tr>
                      <td colspan="2">{{ '::Total' | abpLocalization }}</td>
                      <td class="text-end font-monospace">{{ result()!.totalStockValue | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ result()!.totalGlBalance | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace" [class.text-danger]="!result()!.isMatched">
                        {{ result()!.difference | number:'1.2-2' }}
                      </td>
                      <td></td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            </div>
          </div>
        }
      } @else if (!isLoading()) {
        <div class="text-center py-5 text-muted">
          <i class="fa fa-balance-scale fa-3x mb-3 opacity-25"></i>
          <p>{{ '::ClickCompareToReconcile' | abpLocalization }}</p>
        </div>
      }
    </abp-page>
  `,
})
export class StockGlComparisonComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private fb = inject(FormBuilder);

  companyId = this.fb.control('', Validators.required);
  asOfDate = this.fb.control(new Date().toISOString().split('T')[0]);

  companies = signal<any[]>([]);
  result = signal<StockGlComparisonDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.http.get<any>('/api/app/company', { params: { skipCount: '0', maxResultCount: '100' } })
      .subscribe({
        next: res => {
          this.companies.set(res.items ?? []);
          const defaultId = this.companyContext.currentCompanyId();
          if (defaultId) this.companyId.setValue(defaultId);
        },
        error: () => {},
      });
  }

  runComparison(): void {
    if (!this.companyId.value) {
      this.toaster.warn('::PleaseSelectCompanyFirst');
      return;
    }
    this.isLoading.set(true);
    this.http.get<StockGlComparisonDto>('/api/app/stock-gl-comparison/comparison', {
      params: {
        companyId: this.companyId.value,
        ...(this.asOfDate.value ? { asOfDate: this.asOfDate.value } : {}),
      },
    }).subscribe({
      next: data => {
        this.result.set(data);
        this.isLoading.set(false);
        if (!data.isMatched) {
          this.toaster.warn('::StockGlMismatchDetected');
        } else {
          this.toaster.success('::StockGlMatched');
        }
      },
      error: () => this.isLoading.set(false),
    });
  }

  getMismatchCount(): number {
    return this.result()?.perWarehouse.filter(w => w.hasMismatch).length ?? 0;
  }

  exportCsv(): void {
    const r = this.result();
    if (!r) return;
    const rows = r.perWarehouse.map(wh => ({
      Warehouse: wh.warehouseName,
      StockAccount: wh.stockAccountName ?? '',
      StockValue: wh.stockValue,
      GLBalance: wh.glBalance,
      Difference: wh.difference,
      Status: wh.hasMismatch ? 'Mismatch' : 'Matched',
    }));
    exportToCsv('stock-gl-comparison.csv', rows, ['Warehouse', 'StockAccount', 'StockValue', 'GLBalance', 'Difference', 'Status']);
  }
}
