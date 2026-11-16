import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { AssetReportingService } from '../../../proxy/assets/asset-reporting.service';
import type {
  FixedAssetRegisterReportDto,
  FixedAssetRegisterRowDto,
} from '../../../proxy/assets/models';
import { CompanyService } from '../../../proxy/core/company.service';
import type { CompanyDto } from '../../../proxy/core/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export interface FixedAssetRegisterTotals {
  grossPurchaseAmount: number;
  openingAccumulatedDepreciation: number;
  depreciatedAmount: number;
  revaluationAmount: number;
  assetValue: number;
}

export function filterFixedAssetRows(
  rows: FixedAssetRegisterRowDto[] | undefined,
  searchTerm: string,
): FixedAssetRegisterRowDto[] {
  if (!rows || rows.length === 0) {
    return [];
  }
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return rows;
  }
  return rows.filter(r =>
    [
      r.assetNumber,
      r.assetName,
      r.assetCategoryName,
      r.costCenterName,
      r.location,
      r.status,
    ].some(v => (v ?? '').toLowerCase().includes(term)),
  );
}

export function calculateFixedAssetTotals(rows: FixedAssetRegisterRowDto[]): FixedAssetRegisterTotals {
  return rows.reduce<FixedAssetRegisterTotals>(
    (acc, row) => ({
      grossPurchaseAmount: acc.grossPurchaseAmount + (row.grossPurchaseAmount ?? 0),
      openingAccumulatedDepreciation:
        acc.openingAccumulatedDepreciation + (row.openingAccumulatedDepreciation ?? 0),
      depreciatedAmount: acc.depreciatedAmount + (row.depreciatedAmount ?? 0),
      revaluationAmount: acc.revaluationAmount + (row.revaluationAmount ?? 0),
      assetValue: acc.assetValue + (row.assetValue ?? 0),
    }),
    {
      grossPurchaseAmount: 0,
      openingAccumulatedDepreciation: 0,
      depreciatedAmount: 0,
      revaluationAmount: 0,
      assetValue: 0,
    },
  );
}

@Component({
  selector: 'app-fixed-asset-register',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-book text-primary me-2"></i>
            {{ '::FixedAssetRegister' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::FixedAssetRegisterDescription' | abpLocalization }}
          </p>
        </div>
        @if (filteredRows().length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fas fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Company' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="companyId"
                (ngModelChange)="loadReport()"
              >
                @for (company of companies(); track company.id) {
                  <option [value]="company.id">{{ company.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Status' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="status"
                (ngModelChange)="loadReport()"
              >
                <option value="">{{ '::AllStatuses' | abpLocalization }}</option>
                <option value="In Location">{{ '::InLocation' | abpLocalization }}</option>
                <option value="Disposed">{{ '::Disposed' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::GroupBy' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="groupBy"
                (ngModelChange)="loadReport()"
              >
                <option value="">{{ '::IndividualAssets' | abpLocalization }}</option>
                <option value="Asset Category">{{ '::AssetCategory' | abpLocalization }}</option>
                <option value="Location">{{ '::Location' | abpLocalization }}</option>
                <option value="Cost Center">{{ '::CostCenter' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'::AssetName' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
              />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading() || !companyId"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fas fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Summary KPIs -->
      @if (filteredRows().length > 0) {
        <div class="row g-2 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::GrossPurchaseAmount' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ totals().grossPurchaseAmount | number: '1.2-2' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-warning-subtle text-warning-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::DepreciationAmount' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ totals().depreciatedAmount | number: '1.2-2' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::RevaluationAmount' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ totals().revaluationAmount | number: '1.2-2' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::AssetValue' | abpLocalization }}</div>
              <div class="h5 mb-0 fw-bold">{{ totals().assetValue | number: '1.2-2' }}</div>
            </div>
          </div>
        </div>
      }

      <!-- Register Table -->
      <div class="card border-0 shadow-sm">
        <div class="table-responsive">
          <table class="table table-hover table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                @if (groupBy === '') {
                  <th>{{ '::Assets:AssetNumber' | abpLocalization }}</th>
                  <th>{{ '::AssetName' | abpLocalization }}</th>
                  <th>{{ '::AssetCategory' | abpLocalization }}</th>
                  <th>{{ '::CostCenter' | abpLocalization }}</th>
                  <th>{{ '::Location' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::PurchaseDate' | abpLocalization }}</th>
                } @else if (groupBy === 'Asset Category') {
                  <th>{{ '::AssetCategory' | abpLocalization }}</th>
                } @else if (groupBy === 'Location') {
                  <th>{{ '::Location' | abpLocalization }}</th>
                } @else {
                  <th>{{ '::CostCenter' | abpLocalization }}</th>
                }
                <th class="text-end">{{ '::GrossPurchaseAmount' | abpLocalization }}</th>
                <th class="text-end">{{ '::OpeningAccumulatedDepreciation' | abpLocalization }}</th>
                <th class="text-end">{{ '::DepreciationAmount' | abpLocalization }}</th>
                <th class="text-end">{{ '::RevaluationAmount' | abpLocalization }}</th>
                <th class="text-end">{{ '::AssetValue' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (loading()) {
                <tr>
                  <td [attr.colspan]="groupBy === '' ? 12 : 6" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td [attr.colspan]="groupBy === '' ? 12 : 6" class="text-center py-4 text-muted">
                    {{ '::NoDataAvailable' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track $index) {
                  <tr>
                    @if (groupBy === '') {
                      <td class="fw-semibold">{{ row.assetNumber }}</td>
                      <td>{{ row.assetName }}</td>
                      <td>{{ row.assetCategoryName || '-' }}</td>
                      <td>{{ row.costCenterName || '-' }}</td>
                      <td>{{ row.location || '-' }}</td>
                      <td>
                        <span class="badge bg-light text-dark border">{{ row.status }}</span>
                      </td>
                      <td>{{ row.purchaseDate ? (row.purchaseDate | date: 'yyyy-MM-dd') : '-' }}</td>
                    } @else if (groupBy === 'Asset Category') {
                      <td class="fw-semibold">{{ row.assetCategoryName || '-' }}</td>
                    } @else if (groupBy === 'Location') {
                      <td class="fw-semibold">{{ row.location || '-' }}</td>
                    } @else {
                      <td class="fw-semibold">{{ row.costCenterName || '-' }}</td>
                    }
                    <td class="text-end">{{ (row.grossPurchaseAmount ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.openingAccumulatedDepreciation ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.depreciatedAmount ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.revaluationAmount ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end fw-semibold">{{ (row.assetValue ?? 0) | number: '1.2-2' }}</td>
                  </tr>
                }
              }
            </tbody>
            @if (filteredRows().length > 0) {
              <tfoot class="table-light fw-bold">
                <tr>
                  <td [attr.colspan]="groupBy === '' ? 7 : 1">{{ '::Total' | abpLocalization }}</td>
                  <td class="text-end">{{ totals().grossPurchaseAmount | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().openingAccumulatedDepreciation | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().depreciatedAmount | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().revaluationAmount | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().assetValue | number: '1.2-2' }}</td>
                </tr>
              </tfoot>
            }
          </table>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class FixedAssetRegisterComponent implements OnInit {
  private assetReportingService = inject(AssetReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  companies = signal<CompanyDto[]>([]);
  report = signal<FixedAssetRegisterReportDto | null>(null);
  loading = signal(false);
  searchTerm = signal('');

  companyId = '';
  status = '';
  groupBy = '';

  filteredRows = computed(() =>
    filterFixedAssetRows(this.report()?.rows, this.searchTerm()),
  );

  totals = computed(() => calculateFixedAssetTotals(this.filteredRows()));

  ngOnInit(): void {
    this.companyContext.load();
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        const items = res.items ?? [];
        this.companies.set(items);
        this.companyId = this.companyContext.currentCompanyId() || items[0]?.id || '';
        if (this.companyId) {
          this.loadReport();
        }
      },
    });
  }

  loadReport(): void {
    if (!this.companyId) return;
    this.loading.set(true);

    this.assetReportingService
      .getFixedAssetRegister({
        companyId: this.companyId,
        status: this.status || undefined,
        groupBy: this.groupBy || undefined,
        includeDefaultBookAssets: true,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  exportCsv(): void {
    exportToCsv('fixed-asset-register.csv', this.filteredRows(), [
      'assetNumber',
      'assetName',
      'assetCategoryName',
      'costCenterName',
      'location',
      'status',
      'purchaseDate',
      'grossPurchaseAmount',
      'openingAccumulatedDepreciation',
      'depreciatedAmount',
      'revaluationAmount',
      'assetValue',
    ]);
  }
}
