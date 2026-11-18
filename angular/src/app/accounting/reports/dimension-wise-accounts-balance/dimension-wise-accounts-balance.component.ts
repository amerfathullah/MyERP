import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  DimensionColumnDto,
  DimensionWiseAccountBalanceRowDto,
  DimensionWiseAccountsBalanceReportDto,
} from '../../../proxy/accounting/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildDimensionWiseBalanceCsvRows(
  rows: DimensionWiseAccountBalanceRowDto[] | undefined,
  dimensions: DimensionColumnDto[] | undefined,
): Record<string, string | number>[] {
  const dims = dimensions ?? [];
  return (rows ?? []).map(r => {
    const out: Record<string, string | number> = { Account: r.accountName ?? '' };
    dims.forEach((d, idx) => {
      out[d.dimensionName ?? `Dim ${idx + 1}`] = (r.dimensionBalances?.[idx] ?? 0).toFixed(2);
    });
    out['Total'] = (r.total ?? 0).toFixed(2);
    return out;
  });
}

@Component({
  selector: 'app-dimension-wise-accounts-balance',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './dimension-wise-accounts-balance.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DimensionWiseAccountsBalanceComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    dimension: ['Cost Center'],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0], Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
    financeBook: [''],
    includeDefaultBookEntries: [true],
    showZeroValues: [false],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<DimensionWiseAccountsBalanceReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.companies.set(res.items ?? []);
      const defaultId = this.companyContext.currentCompanyId();
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      if (this.filters.get('companyId')?.value) {
        this.generate();
      }
    });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const {
      companyId,
      dimension,
      fromDate,
      toDate,
      financeBook,
      includeDefaultBookEntries,
      showZeroValues,
    } = this.filters.getRawValue();
    this.reportingService
      .getDimensionWiseAccountsBalance({
        companyId: companyId!,
        dimension: dimension || 'Cost Center',
        fromDate: fromDate!,
        toDate: toDate!,
        financeBook: financeBook?.trim() || undefined,
        includeDefaultBookEntries: includeDefaultBookEntries ?? true,
        showZeroValues: showZeroValues ?? false,
      })
      .subscribe({
        next: data => {
          this.report.set(data);
          this.isLoading.set(false);
        },
        error: err => {
          this.isLoading.set(false);
          this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
        },
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows?.length) return;
    const dimHeaders = (r.dimensions ?? []).map(d => d.dimensionName ?? '');
    exportToCsv(
      'dimension-wise-accounts-balance.csv',
      buildDimensionWiseBalanceCsvRows(r.rows, r.dimensions),
      ['Account', ...dimHeaders, 'Total'],
    );
  }
}
