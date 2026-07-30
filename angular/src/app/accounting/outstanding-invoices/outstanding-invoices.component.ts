import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
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
  imports: [CompanyCurrencyPipe, CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './outstanding-invoices.component.html',
  styleUrls: ['./outstanding-invoices.component.scss'],
})
export class OutstandingInvoicesComponent implements OnInit {
  private salesInvoiceService = inject(SalesInvoiceService);
  private purchaseInvoiceService = inject(PurchaseInvoiceService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  invoices = signal<OutstandingInvoice[]>([]);
  isLoading = signal(false);
  partyType = 'Customer';
  totalOutstanding = signal(0);
  overdueCount = signal(0);

  selectedIds = signal<Set<string>>(new Set());
  isCreatingBatchPayment = signal(false);
  hasSelection = computed(() => this.selectedIds().size > 0);
  selectionCount = computed(() => this.selectedIds().size);
  selectionTotal = computed(() => {
    const ids = this.selectedIds();
    return this.invoices().filter(i => ids.has(i.id)).reduce((s, i) => s + i.outstandingAmount, 0);
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    const service$: any = this.partyType === 'Customer'
      ? this.salesInvoiceService.getList({ companyId, maxResultCount: 500, skipCount: 0 } as any)
      : this.purchaseInvoiceService.getList({ companyId, maxResultCount: 500, skipCount: 0 } as any);

    service$.subscribe({
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

  makePayment(inv: OutstandingInvoice): void {
    const partyType = this.partyType;
    const invoiceType = partyType === 'Customer' ? 'SalesInvoice' : 'PurchaseInvoice';
    this.router.navigate(['/accounting/payments/new'], {
      queryParams: {
        partyType,
        againstInvoiceType: invoiceType,
        againstInvoiceId: inv.id,
        amount: inv.outstandingAmount,
        companyId: this.companyContext.currentCompanyId(),
      },
    });
  }

  sendReminder(inv: OutstandingInvoice): void {
    this.toaster.info(
      this.l.instant('::PaymentReminderSentTo', inv.partyName)
    );
  }

  toggleSelection(inv: OutstandingInvoice): void {
    this.selectedIds.update(ids => {
      const next = new Set(ids);
      if (next.has(inv.id)) { next.delete(inv.id); } else { next.add(inv.id); }
      return next;
    });
  }

  toggleSelectAll(): void {
    const all = this.invoices();
    if (this.selectedIds().size === all.length) {
      this.selectedIds.set(new Set());
    } else {
      this.selectedIds.set(new Set(all.map(i => i.id)));
    }
  }

  isSelected(inv: OutstandingInvoice): boolean {
    return this.selectedIds().has(inv.id);
  }

  clearSelection(): void {
    this.selectedIds.set(new Set());
  }

  createBatchPayment(): void {
    const ids = this.selectedIds();
    if (ids.size === 0) return;
    const selected = this.invoices().filter(i => ids.has(i.id));
    const totalAmount = selected.reduce((s, i) => s + i.outstandingAmount, 0);
    const invoiceType = this.partyType === 'Customer' ? 'SalesInvoice' : 'PurchaseInvoice';

    this.router.navigate(['/accounting/payments/new'], {
      queryParams: {
        partyType: this.partyType,
        amount: totalAmount,
        companyId: this.companyContext.currentCompanyId(),
        batchInvoiceIds: selected.map(i => i.id).join(','),
        batchInvoiceType: invoiceType,
      },
    });
  }

  exportCsv(): void {
    exportToCsv(`outstanding-${this.partyType.toLowerCase()}s.csv`, this.invoices(), [
      'invoiceNumber', 'partyName', 'issueDate', 'dueDate', 'grandTotal', 'outstandingAmount', 'daysOverdue',
    ]);
  }
}
