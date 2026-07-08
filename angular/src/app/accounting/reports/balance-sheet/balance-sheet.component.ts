import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';

interface BalanceSheetRow {
  accountName: string;
  amount: number;
}

@Component({
  selector: 'app-balance-sheet',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule,
    MatCardModule, MatFormFieldModule, MatDatepickerModule,
    MatNativeDateModule, MatInputModule, MatButtonModule,
    MatIconModule, MatDividerModule,
  ],
  templateUrl: './balance-sheet.component.html',
  styleUrls: ['./balance-sheet.component.scss'],
})
export class BalanceSheetComponent {
  private fb = new FormBuilder();

  filters = this.fb.group({
    asOfDate: [new Date()],
  });

  assets: BalanceSheetRow[] = [];
  liabilities: BalanceSheetRow[] = [];
  equity: BalanceSheetRow[] = [];
  totalAssets = 0;
  totalLiabilities = 0;
  totalEquity = 0;

  generate(): void {
    // TODO: Call reporting API
    this.assets = [
      { accountName: 'Cash and Bank', amount: 50000 },
      { accountName: 'Accounts Receivable', amount: 25000 },
      { accountName: 'Inventory', amount: 15000 },
      { accountName: 'Equipment', amount: 80000 },
    ];
    this.liabilities = [
      { accountName: 'Accounts Payable', amount: 12000 },
      { accountName: 'SST Payable', amount: 3500 },
      { accountName: 'Accumulated Depreciation', amount: 8500 },
    ];
    this.equity = [
      { accountName: 'Share Capital', amount: 100000 },
      { accountName: 'Retained Earnings', amount: 46000 },
    ];
    this.totalAssets = this.assets.reduce((s, r) => s + r.amount, 0);
    this.totalLiabilities = this.liabilities.reduce((s, r) => s + r.amount, 0);
    this.totalEquity = this.equity.reduce((s, r) => s + r.amount, 0);
  }
}
