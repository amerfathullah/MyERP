import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { TrialBalanceRowDto, TrialBalanceReportDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-trial-balance',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './trial-balance.component.html',
  styleUrls: ['./trial-balance.component.scss'],
})
export class TrialBalanceComponent {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    asOfDate: [new Date().toISOString().split('T')[0], Validators.required],
  });

  companies = signal<CompanyDto[]>([]);
  data = signal<TrialBalanceRowDto[]>([]);
  totalDebit = signal(0);
  totalCredit = signal(0);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => {
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
    const { companyId, asOfDate } = this.filters.getRawValue();

    this.reportingService.getTrialBalance({
      companyId: companyId!,
      asOfDate: asOfDate!,
    }).subscribe({
      next: (report) => {
        this.data.set(report.rows ?? []);
        this.totalDebit.set(report.totalDebit ?? 0);
        this.totalCredit.set(report.totalCredit ?? 0);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Failed to generate report');
      },
    });
  }
}
