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
  TaxWithholdingDetailRowDto,
  TaxWithholdingDetailsReportDto,
} from '../../proxy/tax/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

export function buildTaxWithholdingDetailsCsvRows(
  rows: TaxWithholdingDetailRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    Date: r.postingDate ?? '',
    'Party Type': r.partyType ?? '',
    Party: r.partyName ?? '',
    TIN: r.tin ?? '',
    'Tax Withholding Category': r.taxCategory ?? '',
    'Rate (%)': (r.rate ?? 0).toFixed(2),
    'Taxable Amount': (r.taxableAmount ?? 0).toFixed(2),
    'Withheld Amount': (r.withheldAmount ?? 0).toFixed(2),
    'Voucher Type': r.voucherType ?? '',
    'Voucher Number': r.voucherNumber ?? '',
    LDC: r.hasLdc ? 'Yes' : 'No',
    'Certificate Number': r.certificateNumber ?? '',
  }));
}

@Component({
  selector: 'app-tax-withholding-details',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './tax-withholding-details.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class TaxWithholdingDetailsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(TaxWithholdingReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    partyType: [''],
    taxCategory: [''],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<TaxWithholdingDetailsReportDto | null>(null);
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
      .getDetails({
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
    exportToCsv('tax-withholding-details.csv', buildTaxWithholdingDetailsCsvRows(rows), [
      'Date',
      'Party Type',
      'Party',
      'TIN',
      'Tax Withholding Category',
      'Rate (%)',
      'Taxable Amount',
      'Withheld Amount',
      'Voucher Type',
      'Voucher Number',
      'LDC',
      'Certificate Number',
    ]);
  }
}
