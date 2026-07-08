import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';

interface BankTransaction {
  id: string;
  date: string;
  description: string;
  amount: number;
  matchedInvoice: string | null;
  isReconciled: boolean;
}

@Component({
  selector: 'app-bank-reconciliation',
  standalone: true,
  imports: [CommonModule, PageModule, MatCardModule, MatTableModule, MatButtonModule, MatIconModule, MatCheckboxModule],
  templateUrl: './bank-reconciliation.component.html',
  styleUrls: ['./bank-reconciliation.component.scss'],
})
export class BankReconciliationComponent {
  transactions: BankTransaction[] = [
    { id: '1', date: '2026-07-01', description: 'Payment from Acme Sdn Bhd', amount: 5300, matchedInvoice: 'INV-2026-0001', isReconciled: true },
    { id: '2', date: '2026-07-03', description: 'Bank charges', amount: -25, matchedInvoice: null, isReconciled: false },
    { id: '3', date: '2026-07-05', description: 'Payment to Supplier XYZ', amount: -12000, matchedInvoice: 'PI-2026-0003', isReconciled: false },
  ];
  displayedColumns = ['date', 'description', 'amount', 'matchedInvoice', 'reconciled', 'actions'];

  reconcile(id: string): void {
    // TODO: Call reconciliation API
    const tx = this.transactions.find(t => t.id === id);
    if (tx) tx.isReconciled = true;
  }

  get unreconciledCount(): number {
    return this.transactions.filter(t => !t.isReconciled).length;
  }

  get totalUnreconciled(): number {
    return this.transactions.filter(t => !t.isReconciled).reduce((s, t) => s + t.amount, 0);
  }
}
