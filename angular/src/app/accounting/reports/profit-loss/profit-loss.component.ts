import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDividerModule } from '@angular/material/divider';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import type { ProfitLossRowDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

@Component({
  selector: 'app-profit-loss',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, LocalizationModule,
    MatCardModule, MatTableModule, MatFormFieldModule,
    MatDatepickerModule, MatNativeDateModule, MatInputModule, MatSelectModule, MatDividerModule,
  ],
  templateUrl: './profit-loss.component.html',
  styleUrls: ['./profit-loss.component.scss'],
})
export class ProfitLossComponent {
  private fb = inject(FormBuilder);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0], Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
  });

  companies = signal<CompanyDto[]>([]);
  revenue = signal<ProfitLossRowDto[]>([]);
  expenses = signal<ProfitLossRowDto[]>([]);
  totalRevenue = signal(0);
  totalExpenses = signal(0);
  netProfit = signal(0);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => this.companies.set(res.items ?? []));
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const { companyId, fromDate, toDate } = this.filters.getRawValue();

    this.reportingService.getProfitLoss({
      companyId: companyId!,
      fromDate: fromDate!,
      toDate: toDate!,
    }).subscribe({
      next: (report) => {
        this.revenue.set(report.revenueRows ?? []);
        this.expenses.set(report.expenseRows ?? []);
        this.totalRevenue.set(report.totalRevenue ?? 0);
        this.totalExpenses.set(report.totalExpense ?? 0);
        this.netProfit.set(report.netProfitOrLoss ?? 0);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Failed to generate report');
      },
    });
  }
}
