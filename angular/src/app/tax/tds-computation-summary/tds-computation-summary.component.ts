import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../proxy/core/company.service';
import { TaxWithholdingReportService } from '../../proxy/tax/tax-withholding-report.service';
import type { CompanyDto } from '../../proxy/core/models';
import type {
  TdsComputationSummaryReportDto,
  TdsComputationSummaryRowDto,
} from '../../proxy/tax/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

export function buildTdsComputationCsvRows(
  rows: TdsComputationSummaryRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    TIN: r.tin ?? '',
    'Party Type': r.partyType ?? '',
    Party: r.partyName ?? '',
    'Tax Withholding Category': r.taxCategory ?? '',
    'Rate (%)': (r.rate ?? 0).toFixed(2),
    LDC: r.hasLdc ? 'Yes' : 'No',
    Transactions: r.transactionCount ?? 0,
    'Total Taxable Amount': (r.totalTaxableAmount ?? 0).toFixed(2),
    'Withheld Amount': (r.totalWithheldAmount ?? 0).toFixed(2),
  }));
}

@Component({
  selector: 'app-tds-computation-summary',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './tds-computation-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class TdsComputationSummaryComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(TaxWithholdingReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    partyType: ['Supplier'],
    taxCategory: [''],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<TdsComputationSummaryReportDto | null>(null);
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
    const { companyId, partyType, taxCategory, fromDate, toDate } = this.filters.getRawValue();
    this.reportService
      .getTdsComputationSummary({
        companyId: companyId!,
        partyType: partyType || undefined,
        taxCategory: taxCategory?.trim() || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
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
    const rows = this.report()?.rows ?? [];
    if (!rows.length) return;
    exportToCsv('tds-computation-summary.csv', buildTdsComputationCsvRows(rows), [
      'TIN',
      'Party Type',
      'Party',
      'Tax Withholding Category',
      'Rate (%)',
      'LDC',
      'Transactions',
      'Total Taxable Amount',
      'Withheld Amount',
    ]);
  }
}
