import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { AssetReportingService } from '../../../proxy/assets/asset-reporting.service';
import type {
  AssetDepreciationsAndBalancesReportDto,
  AssetDepreciationsAndBalancesRowDto,
} from '../../../proxy/assets/models';
import { CompanyService } from '../../../proxy/core/company.service';
import type { CompanyDto } from '../../../proxy/core/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export interface DepreciationBalanceTotals {
  valueAsOnFromDate: number;
  valueOfNewPurchase: number;
  valueOfSoldAsset: number;
  valueOfScrappedAsset: number;
  valueOfCapitalizedAsset: number;
  adjustmentDuringPeriod: number;
  valueAsOnToDate: number;
  accumulatedDepreciationAsOnFromDate: number;
  depreciationAmountDuringThePeriod: number;
  accumulatedDepreciationAsOnToDate: number;
  netAssetValueAsOnFromDate: number;
  netAssetValueAsOnToDate: number;
}

export function filterDepreciationBalanceRows(
  rows: AssetDepreciationsAndBalancesRowDto[] | undefined,
  searchTerm: string,
): AssetDepreciationsAndBalancesRowDto[] {
  if (!rows || rows.length === 0) {
    return [];
  }
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return rows;
  }
  return rows.filter(r =>
    [r.assetCategoryName, r.assetNumber, r.assetName].some(v =>
      (v ?? '').toLowerCase().includes(term),
    ),
  );
}

export function calculateDepreciationBalanceTotals(
  rows: AssetDepreciationsAndBalancesRowDto[],
): DepreciationBalanceTotals {
  return rows.reduce<DepreciationBalanceTotals>(
    (acc, row) => ({
      valueAsOnFromDate: acc.valueAsOnFromDate + (row.valueAsOnFromDate ?? 0),
      valueOfNewPurchase: acc.valueOfNewPurchase + (row.valueOfNewPurchase ?? 0),
      valueOfSoldAsset: acc.valueOfSoldAsset + (row.valueOfSoldAsset ?? 0),
      valueOfScrappedAsset: acc.valueOfScrappedAsset + (row.valueOfScrappedAsset ?? 0),
      valueOfCapitalizedAsset: acc.valueOfCapitalizedAsset + (row.valueOfCapitalizedAsset ?? 0),
      adjustmentDuringPeriod: acc.adjustmentDuringPeriod + (row.adjustmentDuringPeriod ?? 0),
      valueAsOnToDate: acc.valueAsOnToDate + (row.valueAsOnToDate ?? 0),
      accumulatedDepreciationAsOnFromDate:
        acc.accumulatedDepreciationAsOnFromDate + (row.accumulatedDepreciationAsOnFromDate ?? 0),
      depreciationAmountDuringThePeriod:
        acc.depreciationAmountDuringThePeriod + (row.depreciationAmountDuringThePeriod ?? 0),
      accumulatedDepreciationAsOnToDate:
        acc.accumulatedDepreciationAsOnToDate + (row.accumulatedDepreciationAsOnToDate ?? 0),
      netAssetValueAsOnFromDate:
        acc.netAssetValueAsOnFromDate + (row.netAssetValueAsOnFromDate ?? 0),
      netAssetValueAsOnToDate: acc.netAssetValueAsOnToDate + (row.netAssetValueAsOnToDate ?? 0),
    }),
    {
      valueAsOnFromDate: 0,
      valueOfNewPurchase: 0,
      valueOfSoldAsset: 0,
      valueOfScrappedAsset: 0,
      valueOfCapitalizedAsset: 0,
      adjustmentDuringPeriod: 0,
      valueAsOnToDate: 0,
      accumulatedDepreciationAsOnFromDate: 0,
      depreciationAmountDuringThePeriod: 0,
      accumulatedDepreciationAsOnToDate: 0,
      netAssetValueAsOnFromDate: 0,
      netAssetValueAsOnToDate: 0,
    },
  );
}

@Component({
  selector: 'app-asset-depreciations-and-balances',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-balance-scale text-primary me-2"></i>
            {{ '::AssetDepreciationsAndBalances' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::AssetDepreciationsAndBalancesDescription' | abpLocalization }}
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
            <div class="col-md-2">
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
              <label class="form-label small mb-1">{{ '::FromDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="fromDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::ToDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="toDate"
                (change)="loadReport()"
              />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::GroupBy' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="groupBy"
                (ngModelChange)="loadReport()"
              >
                <option value="Asset Category">{{ '::AssetCategory' | abpLocalization }}</option>
                <option value="Asset">{{ '::Asset' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'::AssetCategory' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
              />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading() || !companyId || !fromDate || !toDate"
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

      <!-- Balances Table -->
      <div class="card border-0 shadow-sm">
        <div class="table-responsive">
          <table class="table table-hover table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                @if (groupBy === 'Asset') {
                  <th>{{ '::Assets:AssetNumber' | abpLocalization }}</th>
                  <th>{{ '::AssetName' | abpLocalization }}</th>
                }
                <th>{{ '::AssetCategory' | abpLocalization }}</th>
                <th class="text-end">{{ '::OpeningCost' | abpLocalization }}</th>
                <th class="text-end">{{ '::NewPurchases' | abpLocalization }}</th>
                <th class="text-end">{{ '::SoldAssets' | abpLocalization }}</th>
                <th class="text-end">{{ '::ScrappedAssets' | abpLocalization }}</th>
                <th class="text-end">{{ '::CapitalizedAssets' | abpLocalization }}</th>
                <th class="text-end">{{ '::Adjustments' | abpLocalization }}</th>
                <th class="text-end">{{ '::ClosingCost' | abpLocalization }}</th>
                <th class="text-end">{{ '::OpeningDepreciation' | abpLocalization }}</th>
                <th class="text-end">{{ '::DepreciationDuringPeriod' | abpLocalization }}</th>
                <th class="text-end">{{ '::ClosingDepreciation' | abpLocalization }}</th>
                <th class="text-end">{{ '::NetAssetValue' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (loading()) {
                <tr>
                  <td [attr.colspan]="groupBy === 'Asset' ? 14 : 12" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td [attr.colspan]="groupBy === 'Asset' ? 14 : 12" class="text-center py-4 text-muted">
                    {{ '::NoDataAvailable' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track $index) {
                  <tr>
                    @if (groupBy === 'Asset') {
                      <td class="fw-semibold">{{ row.assetNumber || '-' }}</td>
                      <td>{{ row.assetName || '-' }}</td>
                    }
                    <td class="fw-semibold">{{ row.assetCategoryName || '-' }}</td>
                    <td class="text-end">{{ (row.valueAsOnFromDate ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.valueOfNewPurchase ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.valueOfSoldAsset ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.valueOfScrappedAsset ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.valueOfCapitalizedAsset ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.adjustmentDuringPeriod ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.valueAsOnToDate ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.accumulatedDepreciationAsOnFromDate ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.depreciationAmountDuringThePeriod ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (row.accumulatedDepreciationAsOnToDate ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end fw-semibold">{{ (row.netAssetValueAsOnToDate ?? 0) | number: '1.2-2' }}</td>
                  </tr>
                }
              }
            </tbody>
            @if (filteredRows().length > 0) {
              <tfoot class="table-light fw-bold">
                <tr>
                  <td [attr.colspan]="groupBy === 'Asset' ? 3 : 1">{{ '::Total' | abpLocalization }}</td>
                  <td class="text-end">{{ totals().valueAsOnFromDate | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().valueOfNewPurchase | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().valueOfSoldAsset | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().valueOfScrappedAsset | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().valueOfCapitalizedAsset | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().adjustmentDuringPeriod | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().valueAsOnToDate | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().accumulatedDepreciationAsOnFromDate | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().depreciationAmountDuringThePeriod | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().accumulatedDepreciationAsOnToDate | number: '1.2-2' }}</td>
                  <td class="text-end">{{ totals().netAssetValueAsOnToDate | number: '1.2-2' }}</td>
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
export class AssetDepreciationsAndBalancesComponent implements OnInit {
  private assetReportingService = inject(AssetReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  companies = signal<CompanyDto[]>([]);
  report = signal<AssetDepreciationsAndBalancesReportDto | null>(null);
  loading = signal(false);
  searchTerm = signal('');

  companyId = '';
  fromDate = '';
  toDate = '';
  groupBy = 'Asset Category';

  filteredRows = computed(() =>
    filterDepreciationBalanceRows(this.report()?.rows, this.searchTerm()),
  );

  totals = computed(() => calculateDepreciationBalanceTotals(this.filteredRows()));

  ngOnInit(): void {
    this.companyContext.load();
    const now = new Date();
    const yearStart = new Date(now.getFullYear(), 0, 1);
    this.fromDate = yearStart.toISOString().slice(0, 10);
    this.toDate = now.toISOString().slice(0, 10);

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
    if (!this.companyId || !this.fromDate || !this.toDate) return;
    this.loading.set(true);

    this.assetReportingService
      .getAssetDepreciationsAndBalances({
        companyId: this.companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        groupBy: this.groupBy,
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
    exportToCsv('asset-depreciations-and-balances.csv', this.filteredRows(), [
      'assetNumber',
      'assetName',
      'assetCategoryName',
      'valueAsOnFromDate',
      'valueOfNewPurchase',
      'valueOfSoldAsset',
      'valueOfScrappedAsset',
      'valueOfCapitalizedAsset',
      'adjustmentDuringPeriod',
      'valueAsOnToDate',
      'accumulatedDepreciationAsOnFromDate',
      'depreciationAmountDuringThePeriod',
      'accumulatedDepreciationAsOnToDate',
      'netAssetValueAsOnFromDate',
      'netAssetValueAsOnToDate',
    ]);
  }
}
