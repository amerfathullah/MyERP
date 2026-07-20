import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { exportToCsv } from '../../shared/utils/csv-export';

interface LedgerEntry {
  date: string;
  voucherType: string;
  voucherNumber: string;
  voucherId: string;
  debit: number;
  credit: number;
  balance: number;
}

interface PartyOption {
  id: string;
  name: string;
}

@Component({
  selector: 'app-party-ledger',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './party-ledger.component.html',
  styleUrls: ['./party-ledger.component.scss'],
})
export class PartyLedgerComponent implements OnInit {
  private companyContext = inject(CompanyContextService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);
  private salesInvoiceService = inject(SalesInvoiceService);
  private purchaseInvoiceService = inject(PurchaseInvoiceService);

  entries = signal<LedgerEntry[]>([]);
  parties = signal<PartyOption[]>([]);
  isLoading = signal(false);

  partyType = 'Customer';
  selectedPartyId = '';
  dateFrom = '';
  dateTo = '';
  totalDebit = signal(0);
  totalCredit = signal(0);
  closingBalance = signal(0);

  ngOnInit(): void {
    this.loadParties();
  }

  loadParties(): void {
    const service$: any = this.partyType === 'Customer'
      ? this.customerService.getList({ skipCount: 0, maxResultCount: 200 } as any)
      : this.supplierService.getList({ skipCount: 0, maxResultCount: 200 } as any);

    service$.subscribe({
      next: (result) => {
        this.parties.set(((result as any).items ?? []).map((p: any) => ({
          id: p.id,
          name: p.customerName ?? p.supplierName ?? p.name ?? '—',
        })));
      },
    });
  }

  onPartyTypeChange(): void {
    this.selectedPartyId = '';
    this.entries.set([]);
    this.loadParties();
  }

  loadLedger(): void {
    if (!this.selectedPartyId) return;
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);

    // Fetch invoices and payments for this party
    const service$: any = this.partyType === 'Customer'
      ? this.salesInvoiceService.getList({ companyId, maxResultCount: 500, skipCount: 0 } as any)
      : this.purchaseInvoiceService.getList({ companyId, maxResultCount: 500, skipCount: 0 } as any);

    service$.subscribe({
      next: (result) => {
        const partyField = this.partyType === 'Customer' ? 'customerId' : 'supplierId';
        const partyInvoices = (result.items ?? [])
          .filter((i: any) => i[partyField] === this.selectedPartyId && i.status !== 'Draft' && i.status !== 'Cancelled')
          .filter((i: any) => !this.dateFrom || i.issueDate >= this.dateFrom)
          .filter((i: any) => !this.dateTo || i.issueDate <= this.dateTo);

        const ledger: LedgerEntry[] = [];
        let balance = 0;

        for (const inv of partyInvoices.sort((a: any, b: any) => a.issueDate.localeCompare(b.issueDate))) {
          const isReturn = inv.isReturn;
          const debit = this.partyType === 'Customer' ? (isReturn ? 0 : inv.grandTotal) : (isReturn ? inv.grandTotal : 0);
          const credit = this.partyType === 'Customer' ? (isReturn ? inv.grandTotal : (inv.amountPaid ?? 0)) : (isReturn ? 0 : inv.grandTotal);

          if (debit > 0) {
            balance += debit;
            ledger.push({
              date: inv.issueDate,
              voucherType: isReturn ? 'Credit Note' : (this.partyType === 'Customer' ? 'Sales Invoice' : 'Purchase Invoice'),
              voucherNumber: inv.invoiceNumber,
              voucherId: inv.id,
              debit,
              credit: 0,
              balance,
            });
          }
          if (credit > 0 && !isReturn) {
            balance -= credit;
            ledger.push({
              date: inv.issueDate,
              voucherType: 'Payment',
              voucherNumber: inv.invoiceNumber + ' (paid)',
              voucherId: inv.id,
              debit: 0,
              credit,
              balance,
            });
          }
        }

        this.entries.set(ledger);
        this.totalDebit.set(ledger.reduce((s, e) => s + e.debit, 0));
        this.totalCredit.set(ledger.reduce((s, e) => s + e.credit, 0));
        this.closingBalance.set(balance);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const partyName = this.parties().find(p => p.id === this.selectedPartyId)?.name ?? 'party';
    exportToCsv(`${partyName}-ledger.csv`, this.entries(), [
      'date', 'voucherType', 'voucherNumber', 'debit', 'credit', 'balance',
    ]);
  }
}
