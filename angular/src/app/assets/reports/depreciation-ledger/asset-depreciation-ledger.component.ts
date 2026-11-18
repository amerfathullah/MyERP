import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetService } from '../../../proxy/assets/asset.service';
import { AssetReportingService } from '../../../proxy/assets/asset-reporting.service';
import type {
  AssetCategoryDto,
  AssetDepreciationLedgerReportDto,
  AssetDepreciationLedgerRowDto,
} from '../../../proxy/assets/models';
import { CompanyService } from '../../../proxy/core/company.service';
import type { CompanyDto } from '../../../proxy/core/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../../../accounting/reports/day-book/day-book.component';

export function filterDepreciationLedgerRows(
  rows: AssetDepreciationLedgerRowDto[] | undefined,
  searchTerm: string,
): AssetDepreciationLedgerRowDto[] {
  if (!rows || rows.length === 0) return [];
  const term = searchTerm.trim().toLowerCase();
  if (!term) return rows;
  return rows.filter(r =>
    [r.assetNumber, r.assetName, r.assetCategoryName, r.depreciationEntryNumber, r.costCenterName].some(v =>
      (v ?? '').toLowerCase().includes(term),
    ),
  );
}

@Component({
  selector: 'app-asset-depreciation-ledger',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageModule,
    LocalizationPipe,
  ],
  templateUrl: './asset-depreciation-ledger.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class AssetDepreciationLedgerComponent implements OnInit {
  private router = inject(Router);
  private assetService = inject(AssetService);
  private assetReportingService = inject(AssetReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  categories = signal<AssetCategoryDto[]>([]);
  report = signal<AssetDepreciationLedgerReportDto | null>(null);
  loading = signal(false);
  searchTerm = signal('');

  companyId = '';
  assetCategoryId = '';
  fromDate = '';
  toDate = '';

  filteredRows = computed(() =>
    filterDepreciationLedgerRows(this.report()?.rows, this.searchTerm()),
  );

  totalDepreciation = computed(() =>
    this.filteredRows().reduce((sum, r) => sum + (r.depreciationAmount ?? 0), 0),
  );

  ngOnInit(): void {
    this.companyContext.load();
    const now = new Date();
    const yearStart = new Date(now.getFullYear(), 0, 1);
    this.fromDate = yearStart.toISOString().slice(0, 10);
    this.toDate = now.toISOString().slice(0, 10);

    this.assetService.getCategories().subscribe({
      next: cats => this.categories.set(cats ?? []),
      error: () => {},
    });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        const items = res.items ?? [];
        this.companies.set(items);
        this.companyId = this.companyContext.currentCompanyId() || items[0]?.id || '';
        if (this.companyId) {
          this.loadReport();
        }
      },
      error: () => {},
    });
  }

  loadReport(): void {
    if (!this.companyId || !this.fromDate || !this.toDate) return;
    this.loading.set(true);

    this.assetReportingService
      .getAssetDepreciationLedger({
        companyId: this.companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        assetCategoryId: this.assetCategoryId || null,
        includeDefaultBookAssets: true,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: err => {
          this.loading.set(false);
          this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
        },
      });
  }

  formatDate(value: string | Date | undefined | null): string {
    return formatDayBookVoucherDate(value);
  }

  openAsset(row: AssetDepreciationLedgerRowDto): void {
    if (!row.assetId) return;
    this.router.navigate(['/assets', row.assetId]);
  }

  openJournalEntry(row: AssetDepreciationLedgerRowDto, event: Event): void {
    event.stopPropagation();
    if (!row.depreciationEntryId) return;
    this.router.navigate(['/accounting/journal-entries', row.depreciationEntryId]);
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;

    const data = rows.map(row => ({
      Asset: row.assetNumber ?? '',
      'Asset Name': row.assetName ?? '',
      'Asset Category': row.assetCategoryName ?? '',
      Status: row.status ?? '',
      'Depreciation Date': this.formatDate(row.depreciationDate),
      'Gross Purchase Amount': (row.grossPurchaseAmount ?? 0).toFixed(2),
      'Depreciation Amount': (row.depreciationAmount ?? 0).toFixed(2),
      'Accumulated Depreciation': (row.accumulatedDepreciationAmount ?? 0).toFixed(2),
      'Value After Depreciation': (row.valueAfterDepreciation ?? 0).toFixed(2),
      'Depreciation Entry': row.depreciationEntryNumber ?? '',
      'Purchase Date': this.formatDate(row.purchaseDate),
      'Cost Center': row.costCenterName ?? '',
    }));
    exportToCsv(`asset-depreciation-ledger-${this.fromDate}-to-${this.toDate}.csv`, data, [
      'Asset',
      'Asset Name',
      'Asset Category',
      'Status',
      'Depreciation Date',
      'Gross Purchase Amount',
      'Depreciation Amount',
      'Accumulated Depreciation',
      'Value After Depreciation',
      'Depreciation Entry',
      'Purchase Date',
      'Cost Center',
    ]);
  }
}
