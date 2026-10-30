import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentReconciliationService } from '../../proxy/accounting/payment-reconciliation.service';
import type { OutstandingInvoiceDto, UnreconciledPaymentDto } from '../../proxy/accounting/models';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface OutstandingInvoice {
  accountId?: string;
  voucherId: string;
  voucherType: string;
  documentNumber?: string;
  postingDate?: string;
  grandTotal?: number;
  outstanding: number;
  allocatedAmount: number;
  selected: boolean;
}

@Component({
  selector: 'app-payment-reconciliation',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  templateUrl: './payment-reconciliation.component.html',
})
export class PaymentReconciliationComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private reconciliationService = inject(PaymentReconciliationService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  private companyContext = inject(CompanyContextService);

  partyType = signal<string>('Customer');
  partyId = signal<string>('');
  accountId = signal<string>('');
  invoices = signal<OutstandingInvoice[]>([]);
  payments = signal<UnreconciledPaymentDto[]>([]);
  selectedPaymentVoucherId = signal<string | null>(null);
  loading = signal(false);
  reconciling = signal(false);
  successMessage = signal<string | null>(null);

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParams;
    if (qp['partyType']) {
      this.partyType.set(qp['partyType']);
    }
    if (qp['partyId']) {
      this.partyId.set(qp['partyId']);
    }
    if (qp['accountId']) {
      this.accountId.set(qp['accountId']);
    }
    if (this.partyId()) {
      this.fetchOutstanding();
    }
  }

  fetchOutstanding() {
    if (!this.partyId()) return;
    this.loading.set(true);
    this.successMessage.set(null);
    this.selectedPaymentVoucherId.set(null);

    this.reconciliationService.getOutstandingInvoices(this.partyType(), this.partyId()).subscribe({
      next: (result: OutstandingInvoiceDto[]) => {
        const acctFilter = this.accountId();
        const filtered = acctFilter ? (result ?? []).filter(i => i.accountId === acctFilter) : (result ?? []);
        this.invoices.set(filtered.map(i => ({
          accountId: i.accountId,
          voucherId: i.voucherId!,
          voucherType: i.voucherType ?? 'SalesInvoice',
          outstanding: Math.abs(i.outstanding ?? 0),
          allocatedAmount: 0,
          selected: false,
        })));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.reconciliationService.getUnreconciledPayments(this.partyType(), this.partyId()).subscribe({
      next: (result: UnreconciledPaymentDto[]) => this.payments.set(result ?? []),
      error: () => this.payments.set([]),
    });
  }

  get selectedPayment(): UnreconciledPaymentDto | undefined {
    return this.payments().find(p => p.voucherId === this.selectedPaymentVoucherId());
  }

  selectPayment(voucherId: string) {
    this.selectedPaymentVoucherId.set(voucherId);
    // Deselecting invoices avoids carrying stale allocations across a payment switch —
    // amounts allocated against the previous payment's unallocated cap may exceed the new one.
    this.invoices.update(list => list.map(i => ({ ...i, selected: false, allocatedAmount: 0 })));
  }

  get totalAllocated(): number {
    return this.invoices().filter(i => i.selected).reduce((sum, i) => sum + i.allocatedAmount, 0);
  }

  get overAllocated(): boolean {
    const payment = this.selectedPayment;
    return !!payment && this.totalAllocated > payment.unallocatedAmount + 0.009;
  }

  get totalInvoiceAmount(): number {
    const selected = this.invoices().filter(i => i.selected);
    const list = selected.length > 0 ? selected : this.invoices();
    return list.reduce((sum, i) => sum + (i.outstanding || 0), 0);
  }

  get totalPaymentAmount(): number {
    const selected = this.selectedPayment;
    if (selected) return selected.unallocatedAmount || 0;
    return this.payments().reduce((sum, p) => sum + (p.unallocatedAmount || 0), 0);
  }

  get differenceAmount(): number {
    return Math.round((this.totalInvoiceAmount - this.totalPaymentAmount) * 100) / 100;
  }

  autoAllocating = signal(false);

  autoAllocate() {
    const payment = this.selectedPayment;
    if (!payment) {
      this.toaster.warn('SelectAPaymentFirst');
      return;
    }

    this.autoAllocating.set(true);
    this.reconciliationService.getAutoAllocation(this.partyType(), this.partyId()).subscribe({
      next: plan => {
        const forThisPayment = (plan ?? []).filter(a => a.paymentVoucherId === payment.voucherId);
        this.invoices.update(list => list.map(i => {
          const match = forThisPayment.find(a => a.invoiceVoucherId === i.voucherId);
          return match ? { ...i, selected: true, allocatedAmount: match.allocatedAmount } : i;
        }));
        this.autoAllocating.set(false);
      },
      error: () => this.autoAllocating.set(false),
    });
  }

  reconcile() {
    const payment = this.selectedPayment;
    if (!payment) {
      this.toaster.warn('SelectAPaymentFirst');
      return;
    }

    const selectedInvoices = this.invoices().filter(i => i.selected && i.allocatedAmount > 0);
    const allocations = selectedInvoices.map(i => ({
      paymentVoucherId: payment.voucherId,
      paymentVoucherType: payment.voucherType,
      invoiceVoucherId: i.voucherId,
      invoiceVoucherType: i.voucherType,
      allocatedAmount: i.allocatedAmount,
    }));

    if (allocations.length === 0) return;

    const resolvedAccountId = this.accountId() || selectedInvoices[0]?.accountId || undefined;
    const companyId = this.companyContext.currentCompanyId() || undefined;

    this.reconciling.set(true);
    this.reconciliationService.reconcile({
      partyType: this.partyType(),
      partyId: this.partyId(),
      companyId,
      accountId: resolvedAccountId,
      allocations,
    }).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyReconciled');
        this.successMessage.set(this.l.instant('::SuccessfullyReconciled'));
        this.reconciling.set(false);
        this.fetchOutstanding();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? 'Reconciliation failed');
        this.reconciling.set(false);
      },
    });
  }
}
