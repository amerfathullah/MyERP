import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type { SalesPartnersCommissionReportDto } from '../../../proxy/accounting/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildSalesPartnersCommissionCsvRows(
  report: SalesPartnersCommissionReportDto,
): Record<string, string | number>[] {
  return (report.rows ?? []).map(r => ({
    'Sales Partner': r.salesPartnerName ?? '',
    'Invoices': r.invoiceCount ?? 0,
    'Invoiced Amount (Excl. Tax)': (r.invoicedAmount ?? 0).toFixed(2),
    'Amount Eligible for Commission': (r.amountEligibleForCommission ?? 0).toFixed(2),
    'Total Commission': (r.totalCommission ?? 0).toFixed(2),
    'Average Commission Rate (%)': r.averageCommissionRate !== null && r.averageCommissionRate !== undefined
      ? r.averageCommissionRate.toFixed(2)
      : '',
  }));
}

@Component({
  selector: 'app-sales-partners-commission',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './sales-partners-commission.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class SalesPartnersCommissionComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: [''],
    fromDate: [`${new Date().getFullYear()}-01-01`],
    toDate: [`${new Date().getFullYear()}-12-31`],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<SalesPartnersCommissionReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        const list = res.items ?? [];
        this.companies.set(list);
        const defaultId = this.companyContext.currentCompanyId() || list[0]?.id || '';
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
          this.generate();
        }
      },
      error: () => {},
    });
  }

  generate(): void {
    const val = this.filters.getRawValue();
    if (!val.companyId) {
      this.toaster.warn('Select Company first.');
      return;
    }

    this.isLoading.set(true);
    this.reportingService
      .getSalesPartnersCommission({
        companyId: val.companyId,
        fromDate: val.fromDate || null,
        toDate: val.toDate || null,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: (err: any) => {
          this.toaster.error(err?.error?.error?.message || 'Failed to generate Sales Partners Commission report.');
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows?.length) return;
    const rows = buildSalesPartnersCommissionCsvRows(rep);
    exportToCsv('sales-partners-commission.csv', rows, Object.keys(rows[0]));
  }
}
