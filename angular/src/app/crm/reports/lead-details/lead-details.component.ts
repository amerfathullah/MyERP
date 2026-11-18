import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { CrmReportService } from '../../../proxy/crm/crm-report.service';
import { leadSourceOptions, LeadSource } from '../../../proxy/crm/lead-source.enum';
import { leadStatusOptions, LeadStatus } from '../../../proxy/crm/lead-status.enum';
import type { CompanyDto } from '../../../proxy/core/models';
import type { LeadDetailsReportDto, LeadDetailsRowDto } from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildLeadDetailsCsvRows(
  rows: LeadDetailsRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Lead #': r.leadNumber ?? '',
    'Lead Name': r.leadName ?? '',
    'Job Title': r.jobTitle ?? '',
    Status: r.status ?? '',
    Source: r.source ?? '',
    'UTM Source': r.utmSource ?? '',
    Email: r.email ?? '',
    'Mobile No': r.mobileNo ?? '',
    Phone: r.phone ?? '',
    'Lead Owner': r.leadOwner ?? '',
    Organization: r.organizationName ?? '',
    City: r.city ?? '',
    State: r.state ?? '',
    Country: r.country ?? '',
    Industry: r.industry ?? '',
  }));
}

@Component({
  selector: 'app-lead-details',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './lead-details.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class LeadDetailsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(CrmReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  readonly statusOptions = leadStatusOptions;
  readonly sourceOptions = leadSourceOptions;

  filters = this.fb.group({
    companyId: ['', Validators.required],
    status: [null as LeadStatus | null],
    source: [null as LeadSource | null],
    country: [''],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<LeadDetailsReportDto | null>(null);
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
    const { companyId, status, source, country, fromDate, toDate } = this.filters.getRawValue();
    this.reportService
      .getLeadDetails({
        companyId: companyId!,
        status: status ?? undefined,
        source: source ?? undefined,
        country: country?.trim() || undefined,
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
    exportToCsv('lead-details.csv', buildLeadDetailsCsvRows(rows), [
      'Lead #',
      'Lead Name',
      'Job Title',
      'Status',
      'Source',
      'UTM Source',
      'Email',
      'Mobile No',
      'Phone',
      'Lead Owner',
      'Organization',
      'City',
      'State',
      'Country',
      'Industry',
    ]);
  }
}
