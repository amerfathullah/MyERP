import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentReconciliationService } from '../../proxy/accounting/payment-reconciliation.service';

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

  partyType = signal<string>('Customer');
  partyId = signal<string>('');
  invoices = signal<OutstandingInvoice[]>([]);
  loading = signal(false);
  reconciling = signal(false);
  successMessage = signal<string | null>(null);

  fetchOutstanding() {
    if (!this.partyId()) return;
    this.loading.set(true);
    this.successMessage.set(null);

    this.reconciliationService.getOutstandingInvoices(this.partyType(), this.partyId()).subscribe({
      next: (result) => {
        this.invoices.set((result ?? []).map(i => ({
          voucherId: (i as any).documentId,
          voucherType: (i as any).documentType ?? 'SalesInvoice',
          documentNumber: (i as any).documentNumber,
          postingDate: (i as any).postingDate,
          grandTotal: (i as any).grandTotal,
          outstanding: (i as any).outstandingAmount ?? 0,
          allocatedAmount: 0,
          selected: false,
        })));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  get totalAllocated(): number {
    return this.invoices().filter(i => i.selected).reduce((sum, i) => sum + i.allocatedAmount, 0);
  }

  reconcile() {
    const allocations = this.invoices()
      .filter(i => i.selected && i.allocatedAmount > 0)
      .map(i => ({
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
        this.toaster.success('Payment reconciled successfully');
        this.successMessage.set('Payment reconciled successfully');
        this.reconciling.set(false);
        this.fetchOutstanding();
      },
      error: (err) => {
        this.toaster.error(err?.error?.error?.message ?? 'Reconciliation failed');
        this.reconciling.set(false);
      },
    });
  }
}
