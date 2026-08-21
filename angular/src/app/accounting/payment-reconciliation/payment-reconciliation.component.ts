import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationService } from '@abp/ng.core';
import { PaymentReconciliationService } from '../../proxy/accounting/payment-reconciliation.service';
import type { OutstandingInvoiceDto, UnreconciledPaymentDto } from '../../proxy/accounting/models';

interface OutstandingInvoice {
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
export class PaymentReconciliationComponent {
  private reconciliationService = inject(PaymentReconciliationService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  partyType = signal<string>('Customer');
  partyId = signal<string>('');
  invoices = signal<OutstandingInvoice[]>([]);
  payments = signal<UnreconciledPaymentDto[]>([]);
  selectedPaymentVoucherId = signal<string | null>(null);
  loading = signal(false);
  reconciling = signal(false);
  successMessage = signal<string | null>(null);

  fetchOutstanding() {
    if (!this.partyId()) return;
    this.loading.set(true);
    this.successMessage.set(null);
    this.selectedPaymentVoucherId.set(null);

    this.reconciliationService.getOutstandingInvoices(this.partyType(), this.partyId()).subscribe({
      next: (result: OutstandingInvoiceDto[]) => {
        this.invoices.set((result ?? []).map(i => ({
          voucherId: i.voucherId!,
          voucherType: i.voucherType ?? 'SalesInvoice',
          outstanding: i.outstanding ?? 0,
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

  reconcile() {
    const payment = this.selectedPayment;
    if (!payment) {
      this.toaster.warn('SelectAPaymentFirst');
      return;
    }

    const allocations = this.invoices()
      .filter(i => i.selected && i.allocatedAmount > 0)
      .map(i => ({
        paymentVoucherId: payment.voucherId,
        paymentVoucherType: payment.voucherType,
        invoiceVoucherId: i.voucherId,
        invoiceVoucherType: i.voucherType,
        allocatedAmount: i.allocatedAmount,
      }));

    if (allocations.length === 0) return;

    this.reconciling.set(true);
    this.reconciliationService.reconcile({
      partyType: this.partyType(),
      partyId: this.partyId(),
      allocations,
    } as any).subscribe({
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
