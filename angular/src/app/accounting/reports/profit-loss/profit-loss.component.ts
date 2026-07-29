import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { ProfitLossRowDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-profit-loss',
  standalone: true,
  imports: [CompanyCurrencyPipe, 
    CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './profit-loss.component.html',
  styleUrls: ['./profit-loss.component.scss'],
})
export class ProfitLossComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0], Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
    includeComparison: [false],
  });

  companies = signal<CompanyDto[]>([]);
  revenue = signal<ProfitLossRowDto[]>([]);
  expenses = signal<ProfitLossRowDto[]>([]);
  totalRevenue = signal(0);
  totalExpenses = signal(0);
  netProfit = signal(0);
  isLoading = signal(false);

  // Comparison signals
  previousTotalRevenue = signal<number | null>(null);
  previousTotalExpense = signal<number | null>(null);
  previousNetProfit = signal<number | null>(null);
  previousFromDate = signal<string | null>(null);
  previousToDate = signal<string | null>(null);
  revenueGrowth = signal<number | null>(null);
  expenseGrowth = signal<number | null>(null);
  netProfitGrowth = signal<number | null>(null);

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
    const { companyId, fromDate, toDate, includeComparison } = this.filters.getRawValue();

    this.reportingService.getProfitLoss({
      companyId: companyId!,
      fromDate: fromDate!,
      toDate: toDate!,
      includeComparison: includeComparison ?? false,
    } as any).subscribe({
      next: (report: any) => {
        this.revenue.set(report.revenueRows ?? []);
        this.expenses.set(report.expenseRows ?? []);
        this.totalRevenue.set(report.totalRevenue ?? 0);
        this.totalExpenses.set(report.totalExpense ?? 0);
        this.netProfit.set(report.netProfitOrLoss ?? 0);

        // Comparison data
        this.previousTotalRevenue.set(report.previousTotalRevenue ?? null);
        this.previousTotalExpense.set(report.previousTotalExpense ?? null);
        this.previousNetProfit.set(report.previousNetProfitOrLoss ?? null);
        this.previousFromDate.set(report.previousFromDate ?? null);
        this.previousToDate.set(report.previousToDate ?? null);

        // Calculate growth percentages for totals
        this.revenueGrowth.set(this.calcGrowth(report.totalRevenue, report.previousTotalRevenue));
        this.expenseGrowth.set(this.calcGrowth(report.totalExpense, report.previousTotalExpense));
        this.netProfitGrowth.set(this.calcGrowth(report.netProfitOrLoss, report.previousNetProfitOrLoss));

        this.isLoading.set(false);
      },
      error: (err: any) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
      },
    });
  }

  private calcGrowth(current: number | null, previous: number | null): number | null {
    if (previous === null || previous === undefined) return null;
    if (previous === 0) return current && current > 0 ? 100 : current && current < 0 ? -100 : null;
    return Math.round(((current ?? 0) - previous) / Math.abs(previous) * 1000) / 10;
  }
}
