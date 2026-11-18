import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  ConsolidatedFinancialStatementDto,
  ConsolidatedStatementRowDto,
} from '../../../proxy/accounting/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export const CONSOLIDATED_REPORT_TYPES = [
  { value: 'BalanceSheet', label: 'Balance Sheet' },
  { value: 'ProfitAndLoss', label: 'Profit and Loss Statement' },
  { value: 'CashFlow', label: 'Cash Flow' },
] as const;

export function buildConsolidatedStatementCsvRows(
  report: ConsolidatedFinancialStatementDto,
): Record<string, string | number>[] {
  const cols = report.companyColumns ?? [];
  return (report.rows ?? []).map(row => {
    const prefix = '  '.repeat(row.level ?? 0);
    const label = row.accountCode ? `${row.accountCode} - ${row.accountName ?? ''}` : (row.accountName ?? '');
    const rec: Record<string, string | number> = {
      Section: row.section ?? '',
      Account: `${prefix}${label}`,
    };
    cols.forEach(c => {
      const colHeader = `${c.companyName ?? c.companyId} (${c.currencyCode ?? ''})`;
      rec[colHeader] = ((row.companyAmounts ?? {})[c.companyId ?? ''] ?? 0).toFixed(2);
    });
    rec[`Total (${report.presentationCurrency ?? ''})`] = (row.total ?? 0).toFixed(2);
    return rec;
  });
}

@Component({
  selector: 'app-consolidated-financial-statement',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './consolidated-financial-statement.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ConsolidatedFinancialStatementComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  readonly reportTypes = CONSOLIDATED_REPORT_TYPES;
  readonly currencies = ['MYR', 'USD', 'SGD', 'EUR', 'GBP'] as const;

  filters = this.fb.group({
    companyId: [''],
    reportType: ['BalanceSheet'],
    fromDate: [`${new Date().getFullYear()}-01-01`],
    toDate: [`${new Date().getFullYear()}-12-31`],
    presentationCurrency: [''],
    financeBook: [''],
    includeDefaultBookEntries: [true],
    accumulatedInGroupCompany: [false],
    showZeroValues: [false],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<ConsolidatedFinancialStatementDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: res => {
        const list = res.items ?? [];
        this.companies.set(list);
        const preferred = list.find(c => c.isGroup) ?? list.find(c => c.id === this.companyContext.currentCompanyId()) ?? list[0];
        if (preferred?.id && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: preferred.id });
          this.generate();
        }
      },
      error: () => {},
    });
  }

  generate(): void {
    const val = this.filters.getRawValue();
    if (!val.companyId || !val.fromDate || !val.toDate) {
      this.toaster.warn('Select Company, From Date, and To Date.');
      return;
    }

    this.isLoading.set(true);
    this.reportingService
      .getConsolidatedFinancialStatement({
        companyId: val.companyId,
        reportType: val.reportType || 'BalanceSheet',
        fromDate: val.fromDate,
        toDate: val.toDate,
        presentationCurrency: val.presentationCurrency || null,
        financeBook: val.financeBook?.trim() || null,
        includeDefaultBookEntries: Boolean(val.includeDefaultBookEntries),
        accumulatedInGroupCompany: Boolean(val.accumulatedInGroupCompany),
        showZeroValues: Boolean(val.showZeroValues),
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: (err: any) => {
          this.toaster.error(err?.error?.error?.message || 'Failed to generate Consolidated Financial Statement.');
          this.isLoading.set(false);
        },
      });
  }

  getCellAmount(row: ConsolidatedStatementRowDto, companyId?: string): number {
    if (!companyId || !row.companyAmounts) return 0;
    return row.companyAmounts[companyId] ?? 0;
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows?.length) return;
    const rows = buildConsolidatedStatementCsvRows(rep);
    exportToCsv('consolidated-financial-statement.csv', rows, Object.keys(rows[0]));
  }
}
