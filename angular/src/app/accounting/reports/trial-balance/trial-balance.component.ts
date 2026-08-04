import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { TrialBalanceRowDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-trial-balance',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe, CompanyCurrencyPipe],
  templateUrl: './trial-balance.component.html',
  styleUrls: ['./trial-balance.component.scss'],
})
export class TrialBalanceComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: ['', Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
    includeSubsidiaries: [false],
  });

  companies = signal<CompanyDto[]>([]);
  data = signal<TrialBalanceRowDto[]>([]);
  totalDebit = signal(0);
  totalCredit = signal(0);
  isLoading = signal(false);

  isBalanced = computed(() => Math.abs(this.totalDebit() - this.totalCredit()) < 0.01);
  difference = computed(() => Math.abs(this.totalDebit() - this.totalCredit()));
  accountCount = computed(() => this.data().length);

  ngOnInit(): void {
    const firstOfYear = new Date();
    firstOfYear.setMonth(0, 1);
    this.filters.patchValue({ fromDate: firstOfYear.toISOString().split('T')[0] });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe({
        next: res => {
          this.companies.set(res.items ?? []);
          const defaultId = this.companyContext.currentCompanyId();
          if (defaultId && !this.filters.get('companyId')?.value) {
            this.filters.patchValue({ companyId: defaultId });
          }
          if (this.filters.get('companyId')?.value) {
            this.generate();
          }
        },
        error: () => {},
      });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const { companyId, fromDate, toDate, includeSubsidiaries } = this.filters.getRawValue();

    this.reportingService.getTrialBalance({
      companyId: companyId!,
      asOfDate: toDate!,
      includeSubsidiaries: includeSubsidiaries ?? false,
    }).subscribe({
      next: (report) => {
        this.data.set(report.rows ?? []);
        this.totalDebit.set(report.totalDebit ?? 0);
        this.totalCredit.set(report.totalCredit ?? 0);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
      },
    });
  }

  drillDown(row: TrialBalanceRowDto): void {
    const { companyId, fromDate, toDate } = this.filters.getRawValue();
    this.router.navigate(['/accounting/reports/general-ledger'], {
      queryParams: {
        accountId: row.accountId,
        companyId,
        fromDate,
        toDate,
      },
    });
  }

  getIndentClass(row: TrialBalanceRowDto): string {
    const level = row.level ?? 0;
    return level > 0 ? `ps-${Math.min(level * 3, 5)}` : '';
  }

  isGroupRow(row: TrialBalanceRowDto): boolean {
    return row.isGroup === true;
  }

  exportCsv(): void {
    const rows = this.data().map(r => ({
      'Account Code': r.accountCode ?? '',
      'Account Name': r.accountName ?? '',
      'Type': r.accountType ?? '',
      'Debit': r.debit ?? 0,
      'Credit': r.credit ?? 0,
      'Closing Debit': r.closingDebit ?? 0,
      'Closing Credit': r.closingCredit ?? 0,
    }));
    exportToCsv('trial-balance.csv', rows, ['Account Code', 'Account Name', 'Type', 'Debit', 'Credit', 'Closing Debit', 'Closing Credit']);
  }
}
