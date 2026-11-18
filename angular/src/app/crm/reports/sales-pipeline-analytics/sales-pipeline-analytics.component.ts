import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { CrmReportService } from '../../../proxy/crm/crm-report.service';
import { opportunityStatusOptions, OpportunityStatus } from '../../../proxy/crm/opportunity-status.enum';
import { opportunityTypeOptions, OpportunityType } from '../../../proxy/crm/opportunity-type.enum';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  SalesPipelineAnalyticsReportDto,
  SalesPipelineAnalyticsRowDto,
} from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildPipelineAnalyticsCsvRows(
  rows: SalesPipelineAnalyticsRowDto[] | undefined,
  periodLabels: string[] | undefined,
): Record<string, string | number>[] {
  const labels = periodLabels ?? [];
  return (rows ?? []).map(r => {
    const out: Record<string, string | number> = { Group: r.groupLabel ?? '' };
    labels.forEach((label, i) => {
      out[label] = r.periodValues?.[i] ?? 0;
    });
    out['Total'] = r.total ?? 0;
    return out;
  });
}

@Component({
  selector: 'app-sales-pipeline-analytics',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './sales-pipeline-analytics.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class SalesPipelineAnalyticsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(CrmReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  readonly typeOptions = opportunityTypeOptions;
  readonly statusOptions = opportunityStatusOptions;

  filters = this.fb.group({
    companyId: ['', Validators.required],
    pipelineBy: ['Sales Stage'],
    basedOn: ['Amount'],
    range: ['Monthly'],
    opportunityType: [null as OpportunityType | null],
    status: [null as OpportunityStatus | null],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]],
    toDate: [new Date(new Date().getFullYear(), 11, 31).toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<SalesPipelineAnalyticsReportDto | null>(null);
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
    const { companyId, pipelineBy, basedOn, range, opportunityType, status, fromDate, toDate } =
      this.filters.getRawValue();
    this.reportService
      .getSalesPipelineAnalytics({
        companyId: companyId!,
        pipelineBy: pipelineBy || 'Sales Stage',
        basedOn: basedOn || 'Amount',
        range: range || 'Monthly',
        opportunityType: opportunityType ?? undefined,
        status: status ?? undefined,
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
    exportToCsv('sales-pipeline-analytics.csv', buildPipelineAnalyticsCsvRows(r.rows, labels), [
      'Group',
      ...labels,
      'Total',
    ]);
  }
}
