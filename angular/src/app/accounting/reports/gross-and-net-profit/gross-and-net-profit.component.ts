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
  GrossAndNetProfitReportDto,
  GrossAndNetProfitRowDto,
} from '../../../proxy/accounting/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildGrossAndNetProfitCsvRows(
  report: GrossAndNetProfitReportDto,
): Record<string, string | number>[] {
  const labels = report.periodLabels ?? [];
  return (report.rows ?? []).map(row => {
    const rec: Record<string, string | number> = {
      Account: `${'  '.repeat(row.indent ?? 0)}${row.accountCode ? `${row.accountCode} - ` : ''}${row.accountName ?? ''}`,
    };
    const amounts = row.periodAmounts ?? [];
    labels.forEach((lbl, idx) => {
      rec[lbl] = (amounts[idx] ?? 0).toFixed(2);
    });
    rec['Total'] = (row.total ?? 0).toFixed(2);
    return rec;
  });
}

export const PERIODICITIES = ['Yearly', 'Half-Yearly', 'Quarterly', 'Monthly'] as const;

@Component({
  selector: 'app-gross-and-net-profit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './gross-and-net-profit.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GrossAndNetProfitComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  readonly periodicities = PERIODICITIES;

  filters = this.fb.group({
    companyId: [''],
    fromDate: [`${new Date().getFullYear()}-01-01`],
    toDate: [`${new Date().getFullYear()}-12-31`],
    periodicity: ['Yearly'],
    accumulatedValues: [false],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<GrossAndNetProfitReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      const list = res.items ?? [];
      this.companies.set(list);
      const defaultId = this.companyContext.currentCompanyId() || list[0]?.id || '';
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
        this.generate();
      }
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
      .getGrossAndNetProfit({
        companyId: val.companyId,
        fromDate: val.fromDate,
        toDate: val.toDate,
        periodicity: val.periodicity || 'Yearly',
        accumulatedValues: Boolean(val.accumulatedValues),
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.toaster.error('Failed to generate Gross and Net Profit report.');
          this.isLoading.set(false);
        },
      });
  }

  isHighlightRow(row: GrossAndNetProfitRowDto): boolean {
    return (
      row.rowKind === 'SectionHeader' ||
      row.rowKind === 'GrossProfit' ||
      row.rowKind === 'NetProfit'
    );
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows?.length) return;
    const csvRows = buildGrossAndNetProfitCsvRows(r);
    exportToCsv('gross-and-net-profit', csvRows, Object.keys(csvRows[0]));
  }
}
