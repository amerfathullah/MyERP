import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { SupportReportService } from '../../../proxy/support/support-report.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type { IssueSummaryReportDto, IssueSummaryRowDto } from '../../../proxy/support/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildIssueSummaryCsvRows(
  rows: IssueSummaryRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    Dimension: r.dimensionLabel ?? '',
    Open: r.openCount ?? 0,
    Replied: r.repliedCount ?? 0,
    'On Hold': r.onHoldCount ?? 0,
    Closed: r.closedCount ?? 0,
    Cancelled: r.cancelledCount ?? 0,
    'Total Issues': r.totalIssues ?? 0,
    'SLA Fulfilled': r.slaFulfilledCount ?? 0,
    'SLA Failed': r.slaFailedCount ?? 0,
    'First Response Due': r.firstResponseDueCount ?? 0,
    'Resolution Due': r.resolutionDueCount ?? 0,
    'Avg First Response (h)': (r.avgFirstResponseTimeHours ?? 0).toFixed(2),
    'Avg Hold (h)': (r.avgHoldTimeHours ?? 0).toFixed(2),
    'Avg Resolution (h)': (r.avgResolutionTimeHours ?? 0).toFixed(2),
  }));
}

@Component({
  selector: 'app-issue-summary',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './issue-summary.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class IssueSummaryComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(SupportReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    basedOn: ['Customer'],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
    priority: [''],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<IssueSummaryReportDto | null>(null);
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
    const { companyId, basedOn, fromDate, toDate, priority } = this.filters.getRawValue();
    this.reportService
      .getIssueSummary({
        companyId: companyId!,
        basedOn: basedOn || 'Customer',
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        priority: priority || undefined,
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
    exportToCsv('issue-summary.csv', buildIssueSummaryCsvRows(rows), [
      'Dimension',
      'Open',
      'Replied',
      'On Hold',
      'Closed',
      'Cancelled',
      'Total Issues',
      'SLA Fulfilled',
      'SLA Failed',
      'First Response Due',
      'Resolution Due',
      'Avg First Response (h)',
      'Avg Hold (h)',
      'Avg Resolution (h)',
    ]);
  }
}
