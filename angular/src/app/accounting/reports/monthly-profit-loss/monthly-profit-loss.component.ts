import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { MonthlyProfitLossReportDto, MonthlyProfitLossRowDto } from '../../../proxy/accounting/models';

@Component({
  selector: 'app-monthly-profit-loss',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, CompanyCurrencyPipe],
  templateUrl: './monthly-profit-loss.component.html',
})
export class MonthlyProfitLossComponent implements OnInit {
  private reportingService = inject(ReportingService);
  private toaster = inject(ToasterService);
  companyContext = inject(CompanyContextService);

  report = signal<MonthlyProfitLossReportDto | null>(null);
  isLoading = signal(false);
  selectedYear = new Date().getFullYear();
  startMonth = 1;

  ngOnInit(): void {
    this.companyContext.load();
    setTimeout(() => this.generate(), 300);
  }

  generate(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.reportingService.getMonthlyProfitLoss({
      companyId,
      year: this.selectedYear,
      startMonth: this.startMonth,
    }).subscribe({
      next: (data) => {
        this.report.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toaster.error('::FailedToGenerateReport');
      },
    });
  }

  getNetProfitClass(amount: number): string {
    return amount >= 0 ? 'text-success fw-bold' : 'text-danger fw-bold';
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;

    const headers = ['Account Code', 'Account Name', 'Type', ...r.monthLabels!, 'Annual Total'];
    const allRows = [...(r.revenueRows || []), ...(r.expenseRows || [])];
    const rows = allRows.map(row => [
      row.accountCode,
      row.accountName,
      row.accountType,
      ...row.monthlyAmounts!.map(a => a.toFixed(2)),
      row.annualTotal?.toFixed(2),
    ]);

    rows.push(['', 'Total Revenue', '', ...r.monthlyRevenue!.map(a => a.toFixed(2)), r.annualRevenue?.toFixed(2)]);
    rows.push(['', 'Total Expense', '', ...r.monthlyExpense!.map(a => a.toFixed(2)), r.annualExpense?.toFixed(2)]);
    rows.push(['', 'Net Profit', '', ...r.monthlyNetProfit!.map(a => a.toFixed(2)), r.annualNetProfit?.toFixed(2)]);

    exportToCsv(`monthly-pl-${this.selectedYear}.csv`, rows, headers);
  }
}
