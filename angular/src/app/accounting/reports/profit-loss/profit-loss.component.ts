import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

interface PnlRow {
  accountName: string;
  amount: number;
}

@Component({
  selector: 'app-profit-loss',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule,
    MatCardModule, MatTableModule, MatFormFieldModule,
    MatDatepickerModule, MatNativeDateModule, MatInputModule,
    MatButtonModule, MatIconModule,
  ],
  templateUrl: './profit-loss.component.html',
  styleUrls: ['./profit-loss.component.scss'],
})
export class ProfitLossComponent {
  private fb = new FormBuilder();

  filters = this.fb.group({
    fromDate: [new Date(new Date().getFullYear(), 0, 1)],
    toDate: [new Date()],
  });

  revenue: PnlRow[] = [];
  expenses: PnlRow[] = [];
  totalRevenue = 0;
  totalExpenses = 0;
  netProfit = 0;

  generate(): void {
    // TODO: Call reporting API
    this.revenue = [
      { accountName: 'Sales Revenue', amount: 200000 },
      { accountName: 'Service Revenue', amount: 50000 },
    ];
    this.expenses = [
      { accountName: 'Cost of Goods Sold', amount: 120000 },
      { accountName: 'Operating Expenses', amount: 35000 },
      { accountName: 'Salaries & Wages', amount: 58000 },
      { accountName: 'Depreciation', amount: 8500 },
    ];
    this.totalRevenue = this.revenue.reduce((s, r) => s + r.amount, 0);
    this.totalExpenses = this.expenses.reduce((s, r) => s + r.amount, 0);
    this.netProfit = this.totalRevenue - this.totalExpenses;
  }
}
