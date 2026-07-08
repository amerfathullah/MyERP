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

interface TrialBalanceRow {
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
}

@Component({
  selector: 'app-trial-balance',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule,
    MatCardModule, MatTableModule, MatFormFieldModule,
    MatDatepickerModule, MatNativeDateModule, MatInputModule,
    MatButtonModule, MatIconModule,
  ],
  templateUrl: './trial-balance.component.html',
  styleUrls: ['./trial-balance.component.scss'],
})
export class TrialBalanceComponent {
  private fb = new FormBuilder();

  filters = this.fb.group({
    fromDate: [new Date(new Date().getFullYear(), 0, 1)],
    toDate: [new Date()],
  });

  displayedColumns = ['accountCode', 'accountName', 'debit', 'credit'];
  data: TrialBalanceRow[] = [];
  totalDebit = 0;
  totalCredit = 0;

  generate(): void {
    // TODO: Call reporting API
    // Mock data
    this.data = [
      { accountCode: '1110', accountName: 'Cash and Bank', debit: 50000, credit: 0 },
      { accountCode: '1120', accountName: 'Accounts Receivable', debit: 25000, credit: 0 },
      { accountCode: '2100', accountName: 'Accounts Payable', debit: 0, credit: 12000 },
      { accountCode: '3100', accountName: 'Share Capital', debit: 0, credit: 100000 },
      { accountCode: '4100', accountName: 'Sales Revenue', debit: 0, credit: 200000 },
      { accountCode: '5100', accountName: 'Cost of Goods Sold', debit: 120000, credit: 0 },
      { accountCode: '5200', accountName: 'Operating Expenses', debit: 35000, credit: 0 },
      { accountCode: '3200', accountName: 'Retained Earnings', debit: 0, credit: 18000 },
      { accountCode: '2200', accountName: 'SST Payable', debit: 0, credit: 3500 },
      { accountCode: '1130', accountName: 'Inventory', debit: 15000, credit: 0 },
      { accountCode: '1210', accountName: 'Equipment', debit: 80000, credit: 0 },
      { accountCode: '4200', accountName: 'Service Revenue', debit: 0, credit: 50000 },
      { accountCode: '5300', accountName: 'Depreciation', debit: 8500, credit: 0 },
      { accountCode: '2300', accountName: 'Accumulated Depreciation', debit: 0, credit: 8500 },
      { accountCode: '5400', accountName: 'Salaries & Wages', debit: 58000, credit: 0 },
    ];
    this.totalDebit = this.data.reduce((s, r) => s + r.debit, 0);
    this.totalCredit = this.data.reduce((s, r) => s + r.credit, 0);
  }
}
