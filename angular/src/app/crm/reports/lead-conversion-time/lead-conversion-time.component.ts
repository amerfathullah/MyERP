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
import type {
  LeadConversionTimeReportDto,
  LeadConversionTimeRowDto,
} from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildLeadConversionCsvRows(
  rows: LeadConversionTimeRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    'Lead #': r.leadNumber ?? '',
    'Lead Name': r.leadName ?? '',
    Customer: r.customerName ?? '',
    Interactions: r.interactions ?? 0,
    'Duration (Days)': (r.durationInDays ?? 0).toFixed(2),
    'Support Tickets': r.supportTickets ?? 0,
    'Converted On': r.convertedOn ?? '',
  }));
}

@Component({
  selector: 'app-lead-conversion-time',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './lead-conversion-time.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class LeadConversionTimeComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(CrmReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<LeadConversionTimeReportDto | null>(null);
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
      .getLeadConversionTime({
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
    exportToCsv('lead-conversion-time.csv', buildLeadConversionCsvRows(rows), [
      'Lead #',
      'Lead Name',
      'Customer',
      'Interactions',
      'Duration (Days)',
      'Support Tickets',
      'Converted On',
    ]);
  }
}
