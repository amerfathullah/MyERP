import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface OutstandingInvoice {
  id: string;
  invoiceNumber: string;
  partyName: string;
  issueDate: string;
  dueDate: string;
  grandTotal: number;
  outstandingAmount: number;
  daysOverdue: number;
}

@Component({
  selector: 'app-outstanding-invoices',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './outstanding-invoices.component.html',
  styleUrls: ['./outstanding-invoices.component.scss'],
})
export class OutstandingInvoicesComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  invoices = signal<OutstandingInvoice[]>([]);
  isLoading = signal(false);
  partyType = 'Customer';
  totalOutstanding = signal(0);
  overdueCount = signal(0);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    const endpoint = this.partyType === 'Customer'
      ? '/api/app/sales-invoice'
      : '/api/app/purchase-invoice';

    this.http.get<any>(endpoint, {
      params: { companyId, maxResultCount: '500', skipCount: '0' },
    }).subscribe({
      next: (result) => {
        const today = new Date();
        const outstanding = (result.items ?? [])
          .filter((i: any) => i.status === 'Posted' && (i.grandTotal - (i.amountPaid ?? 0)) > 0.01)
          .map((i: any) => {
            const dueDate = new Date(i.dueDate ?? i.issueDate);
            const daysOverdue = Math.max(0, Math.floor((today.getTime() - dueDate.getTime()) / 86400000));
            return {
              id: i.id,
              invoiceNumber: i.invoiceNumber,
              partyName: i.customerName ?? i.supplierName ?? '—',
              issueDate: i.issueDate,
              dueDate: i.dueDate ?? i.issueDate,
              grandTotal: i.grandTotal,
              outstandingAmount: i.grandTotal - (i.amountPaid ?? 0),
              daysOverdue,
            } as OutstandingInvoice;
          })
          .sort((a: OutstandingInvoice, b: OutstandingInvoice) => b.daysOverdue - a.daysOverdue);

        this.invoices.set(outstanding);
        this.totalOutstanding.set(outstanding.reduce((s: number, i: OutstandingInvoice) => s + i.outstandingAmount, 0));
        this.overdueCount.set(outstanding.filter((i: OutstandingInvoice) => i.daysOverdue > 0).length);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  onPartyTypeChange(): void {
    this.loadData();
  }

  exportCsv(): void {
    exportToCsv(`outstanding-${this.partyType.toLowerCase()}s.csv`, this.invoices(), [
      'invoiceNumber', 'partyName', 'issueDate', 'dueDate', 'grandTotal', 'outstandingAmount', 'daysOverdue',
    ]);
  }
}
