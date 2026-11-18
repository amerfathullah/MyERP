import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { CrmReportService } from '../../../proxy/crm/crm-report.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type { ProspectsEngagedReportDto, ProspectsEngagedRowDto } from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildProspectsEngagedCsvRows(
  rows: ProspectsEngagedRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Lead #': r.leadNumber ?? '',
    'Lead Name': r.leadName ?? '',
    Organization: r.organizationName ?? '',
    'Reference Type': r.referenceType ?? '',
    'Reference Name': r.referenceName ?? '',
    Interactions: r.interactionCount ?? 0,
    'Last Communication': r.lastCommunication ?? '',
    'Last Communication Date': r.lastCommunicationDate ?? '',
  }));
}

@Component({
  selector: 'app-prospects-engaged',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './prospects-engaged.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ProspectsEngagedComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(CrmReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    leadAgeDays: [60, [Validators.min(1)]],
    minInteractions: [1, [Validators.min(1)]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<ProspectsEngagedReportDto | null>(null);
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
    const { companyId, leadAgeDays, minInteractions } = this.filters.getRawValue();
    this.reportService
      .getProspectsEngagedButNotConverted({
        companyId: companyId!,
        leadAgeDays: Number(leadAgeDays) || 60,
        minInteractions: Number(minInteractions) || 1,
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
    exportToCsv('prospects-engaged-but-not-converted.csv', buildProspectsEngagedCsvRows(rows), [
      'Lead #',
      'Lead Name',
      'Organization',
      'Reference Type',
      'Reference Name',
      'Interactions',
      'Last Communication',
      'Last Communication Date',
    ]);
  }
}
