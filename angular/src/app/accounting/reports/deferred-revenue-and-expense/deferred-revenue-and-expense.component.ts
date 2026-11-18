import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { DeferredReportingService } from '../../../proxy/accounting/deferred-reporting.service';
import { FiscalYearService } from '../../../proxy/accounting/fiscal-year.service';
import type { CompanyDto } from '../../../proxy/core/models';
import type {
  DeferredReportPeriodDto,
  DeferredRevenueAndExpenseReportDto,
  DeferredRevenueAndExpenseRowDto,
  FiscalYearDto,
} from '../../../proxy/accounting/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function buildDeferredRevenueCsvRows(
  rows: DeferredRevenueAndExpenseRowDto[] | undefined,
  periods: DeferredReportPeriodDto[] | undefined,
): Record<string, string | number>[] {
  const periodList = periods ?? [];
  return (rows ?? []).map(r => {
    const out: Record<string, string | number> = {
      Name: r.name ?? '',
      'Service Start Date': r.serviceStartDate ? r.serviceStartDate.split('T')[0] : '',
      'Service End Date': r.serviceEndDate ? r.serviceEndDate.split('T')[0] : '',
      Amount: (r.amount ?? 0).toFixed(2),
    };
    periodList.forEach((p, idx) => {
      out[p.label ?? `Period ${idx + 1}`] = (r.periodValues?.[idx] ?? 0).toFixed(2);
    });
    return out;
  });
}

@Component({
  selector: 'app-deferred-revenue-and-expense',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './deferred-revenue-and-expense.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class DeferredRevenueAndExpenseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private deferredReportingService = inject(DeferredReportingService);
  private fiscalYearService = inject(FiscalYearService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private currentYear = new Date().getFullYear();

  filters = this.fb.group({
    companyId: ['', Validators.required],
    filterBasedOn: ['Fiscal Year'],
    fromFiscalYearId: [''],
    toFiscalYearId: [''],
    periodStartDate: [`${this.currentYear}-01-01`],
    periodEndDate: [`${this.currentYear}-12-31`],
    periodicity: ['Monthly'],
    type: ['Revenue'],
    withUpcomingPostings: [true],
  });

  companies = signal<CompanyDto[]>([]);
  allFiscalYears = signal<FiscalYearDto[]>([]);
  companyFiscalYears = computed(() => {
    const cid = this.filters.get('companyId')?.value;
    return this.allFiscalYears().filter(f => !cid || f.companyId === cid);
  });
  report = signal<DeferredRevenueAndExpenseReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.companies.set(res.items ?? []);
      const defaultId = this.companyContext.currentCompanyId();
      if (defaultId && !this.filters.get('companyId')?.value) {
        this.filters.patchValue({ companyId: defaultId });
      }
      this.loadFiscalYears();
    });
  }

  loadFiscalYears(): void {
    this.fiscalYearService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      this.allFiscalYears.set(res.items ?? []);
      const matching = this.companyFiscalYears();
      if (matching.length > 0 && !this.filters.get('fromFiscalYearId')?.value) {
        this.filters.patchValue({
          fromFiscalYearId: matching[0].id ?? '',
          toFiscalYearId: matching[0].id ?? '',
        });
      }
      if (this.filters.get('companyId')?.value) {
        this.generate();
      }
    });
  }

  onCompanyChange(): void {
    const matching = this.companyFiscalYears();
    const firstId = matching[0]?.id ?? '';
    this.filters.patchValue({ fromFiscalYearId: firstId, toFiscalYearId: firstId });
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const v = this.filters.getRawValue();
    this.deferredReportingService
      .getDeferredRevenueAndExpense({
        companyId: v.companyId!,
        filterBasedOn: v.filterBasedOn || 'Date Range',
        fromFiscalYearId: v.filterBasedOn === 'Fiscal Year' ? v.fromFiscalYearId || null : null,
        toFiscalYearId: v.filterBasedOn === 'Fiscal Year' ? v.toFiscalYearId || null : null,
        periodStartDate: v.periodStartDate || null,
        periodEndDate: v.periodEndDate || null,
        periodicity: v.periodicity || 'Monthly',
        type: v.type || 'Revenue',
        withUpcomingPostings: v.withUpcomingPostings ?? true,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: err => {
          this.toaster.error(err?.error?.error?.message || 'Error generating report');
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.rows?.length) return;
    const periodHeaders = (r.periods ?? []).map(p => p.label ?? '');
    exportToCsv(
      'deferred-revenue-and-expense.csv',
      buildDeferredRevenueCsvRows(r.rows, r.periods),
      ['Name', 'Service Start Date', 'Service End Date', 'Amount', ...periodHeaders],
    );
  }
}
