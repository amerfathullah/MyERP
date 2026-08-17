import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { PaymentOrderService } from '../../proxy/accounting/payment-order.service';
import type { PaymentOrderDto } from '../../proxy/accounting/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

const STATUS_LABELS = ['Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled'];

@Component({
  selector: 'app-payment-order-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::PaymentOrders' | abpLocalization">
      <app-breadcrumb />
      @if (d) {
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start">
            <h5>{{ d.orderNumber }}</h5>
            <span class="badge bg-info">{{ statusLabel(d.status) }}</span>
          </div>
          <div class="row mt-3">
            <div class="col-md-3"><strong>{{ '::PostingDate' | abpLocalization }}:</strong> {{ d.postingDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-3"><strong>{{ '::Type' | abpLocalization }}:</strong> {{ d.paymentOrderType === 0 ? ('::PaymentRequest' | abpLocalization) : ('::PaymentEntry' | abpLocalization) }}</div>
          </div>

          <table class="table table-sm mt-3">
            <thead><tr><th>{{ '::ReferenceType' | abpLocalization }}</th><th>{{ '::ReferenceId' | abpLocalization }}</th><th>{{ 'Amount' | abpLocalization }}</th><th>{{ '::ModeOfPayment' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (r of d.references; track r.id) {
                <tr><td>{{ r.referenceType }}</td><td>{{ r.referenceId }}</td><td>{{ r.amount }}</td><td>{{ r.modeOfPayment ?? '—' }}</td></tr>
              }
            </tbody>
          </table>

          <div class="mt-3 d-flex gap-2">
            @if (d.status === 0) { <button class="btn btn-sm btn-success" (click)="submit()"><i class="fa fa-check me-1"></i>{{ 'Submit' | abpLocalization }}</button> }
            @if (d.status === 1) {
              @for (supplierId of distinctSuppliers(); track supplierId) {
                <button class="btn btn-sm btn-primary" (click)="makePaymentRecords(supplierId)">
                  <i class="fa fa-file-invoice-dollar me-1"></i>{{ '::MakePaymentRecords' | abpLocalization }}
                </button>
              }
              <button class="btn btn-sm btn-outline-danger" (click)="cancel()"><i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            }
          </div>
        </div></div>
      }
    </abp-page>
  `,
})
export class PaymentOrderDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(PaymentOrderService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  d: PaymentOrderDto | null = null;

  ngOnInit(): void { this.load(); }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: (r) => this.d = r, error: () => {} });
  }

  statusLabel(status: number | undefined): string { return STATUS_LABELS[status ?? 0] ?? 'Draft'; }

  distinctSuppliers(): string[] {
    const ids = (this.d?.references ?? []).map((r) => r.supplierId).filter((x): x is string => !!x);
    return Array.from(new Set(ids));
  }

  submit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.submit(id).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.load(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Submit failed'),
    });
  }

  cancel(): void {
    this.confirmation.warn('::AreYouSureToCancel', '::AreYouSure').subscribe((status) => {
      if (status === 'confirm') {
        const id = this.route.snapshot.paramMap.get('id')!;
        this.service.cancel(id).subscribe({
          next: () => { this.toaster.success('::SuccessfullyCancelled'); this.load(); },
          error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Cancel failed'),
        });
      }
    });
  }

  makePaymentRecords(supplierId: string): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    const modeOfPayment = (this.d?.references ?? []).find((r) => r.supplierId === supplierId)?.modeOfPayment ?? undefined;
    this.service.makePaymentRecords(id, { supplierId, modeOfPayment }).subscribe({
      next: () => this.toaster.success('::JournalEntryCreated'),
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
