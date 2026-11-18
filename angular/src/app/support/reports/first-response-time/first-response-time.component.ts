import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { SupportReportService } from '../../../proxy/support/support-report.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type { FirstResponseTimeReportDto, FirstResponseTimeRowDto } from '../../../proxy/support/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildFirstResponseTimeCsvRows(
  rows: FirstResponseTimeRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    Date: r.date ?? '',
    'Issue Count': r.issueCount ?? 0,
    'Avg First Response (h)': (r.avgFirstResponseTimeHours ?? 0).toFixed(2),
  }));
}

@Component({
  selector: 'app-first-response-time',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './first-response-time.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class FirstResponseTimeComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(SupportReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<FirstResponseTimeReportDto | null>(null);
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
    const { companyId, fromDate, toDate } = this.filters.getRawValue();
    this.reportService
      .getFirstResponseTime({
        companyId: companyId!,
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
    exportToCsv('first-response-time.csv', buildFirstResponseTimeCsvRows(rows), [
      'Date',
      'Issue Count',
      'Avg First Response (h)',
    ]);
  }
}
