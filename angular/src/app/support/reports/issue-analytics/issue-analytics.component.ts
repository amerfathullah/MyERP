import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { SupportReportService } from '../../../proxy/support/support-report.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type { IssueAnalyticsReportDto, IssueAnalyticsRowDto } from '../../../proxy/support/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildIssueAnalyticsCsvRows(
  rows: IssueAnalyticsRowDto[] | undefined,
  periodLabels: string[] | undefined,
): Record<string, string | number>[] {
  const labels = periodLabels ?? [];
  return (rows ?? []).map(r => {
    const out: Record<string, string | number> = { Dimension: r.dimensionLabel ?? '' };
    labels.forEach((label, i) => {
      out[label] = r.periodCounts?.[i] ?? 0;
    });
    out['Total'] = r.total ?? 0;
    return out;
  });
}

@Component({
  selector: 'app-issue-analytics',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './issue-analytics.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class IssueAnalyticsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(SupportReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    basedOn: ['Customer'],
    range: ['Monthly'],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<IssueAnalyticsReportDto | null>(null);
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
    const { companyId, basedOn, range, fromDate, toDate } = this.filters.getRawValue();
    this.reportService
      .getIssueAnalytics({
        companyId: companyId!,
        basedOn: basedOn || 'Customer',
        range: range || 'Monthly',
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
    const r = this.report();
    if (!r?.rows?.length) return;
    const labels = r.periodLabels ?? [];
    exportToCsv('issue-analytics.csv', buildIssueAnalyticsCsvRows(r.rows, labels), [
      'Dimension',
      ...labels,
      'Total',
    ]);
  }
}
