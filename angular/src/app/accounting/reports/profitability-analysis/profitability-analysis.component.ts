import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type {
  ProfitabilityAnalysisReportDto,
  ProfitabilityAnalysisRowDto,
} from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

export function calculateGrossMarginPercent(totalIncome: number, totalGrossProfit: number): number {
  if (!totalIncome || totalIncome === 0) {
    return 0;
  }
  return Math.round((totalGrossProfit / totalIncome) * 10000) / 100;
}

@Component({
  selector: 'app-profitability-analysis',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
    CompanyCurrencyPipe,
  ],
  templateUrl: './profitability-analysis.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ProfitabilityAnalysisComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private readonly firstOfYear = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];
  private readonly today = new Date().toISOString().split('T')[0];

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [this.firstOfYear, Validators.required],
    toDate: [this.today, Validators.required],
    basedOn: ['CostCenter', Validators.required],
    financeBook: [''],
    includeDefaultBookEntries: [true],
    showZeroValues: [false],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<ProfitabilityAnalysisReportDto | null>(null);
  isLoading = signal(false);

  rows = computed(() => this.report()?.rows ?? []);
  grossMarginPercent = computed(() =>
    calculateGrossMarginPercent(
      this.report()?.totalIncome ?? 0,
      this.report()?.totalGrossProfitOrLoss ?? 0,
    ),
  );

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      },
      error: () => {},
    });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const val = this.filters.getRawValue();
    this.reportingService.getProfitabilityAnalysis({
      companyId: val.companyId!,
      fromDate: val.fromDate!,
      toDate: val.toDate!,
      basedOn: val.basedOn ?? 'CostCenter',
      financeBook: val.financeBook?.trim() || null,
      includeDefaultBookEntries: val.includeDefaultBookEntries ?? true,
      showZeroValues: val.showZeroValues ?? false,
    }).subscribe({
      next: res => {
        this.report.set(res);
        this.isLoading.set(false);
      },
      error: err => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
      },
    });
  }

  getIndentClass(row: ProfitabilityAnalysisRowDto): string {
    const level = row.level ?? 0;
    return level > 0 ? `ps-${Math.min(level * 3, 5)}` : '';
  }

  exportCsv(): void {
    const rows = this.rows();
    if (rows.length === 0) return;
    const data = rows.map(r => ({
      Code: r.dimensionCode ?? '',
      Name: r.dimensionName ?? '',
      Income: (r.income ?? 0).toFixed(2),
      Expense: (r.expense ?? 0).toFixed(2),
      'Gross Profit / Loss': (r.grossProfitOrLoss ?? 0).toFixed(2),
    }));
    const rep = this.report();
    data.push({
      Code: '',
      Name: 'TOTAL',
      Income: (rep?.totalIncome ?? 0).toFixed(2),
      Expense: (rep?.totalExpense ?? 0).toFixed(2),
      'Gross Profit / Loss': (rep?.totalGrossProfitOrLoss ?? 0).toFixed(2),
    });
    exportToCsv('profitability-analysis.csv', data, [
      'Code',
      'Name',
      'Income',
      'Expense',
      'Gross Profit / Loss',
    ]);
  }
}
