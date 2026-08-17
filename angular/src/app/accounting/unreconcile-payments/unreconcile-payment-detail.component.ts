import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { UnreconcilePaymentService } from '../../proxy/accounting/unreconcile-payment.service';
import type { UnreconcilePaymentDto } from '../../proxy/accounting/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

const STATUS_LABELS = ['Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled'];
const VOUCHER_TYPE_LABELS = ['Payment Entry', 'Journal Entry'];

@Component({
  selector: 'app-unreconcile-payment-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::UnreconcilePayments' | abpLocalization">
      <app-breadcrumb />
      @if (d) {
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start">
            <h5>{{ voucherTypeLabel(d.voucherType) }} — {{ d.voucherId }}</h5>
            <span class="badge bg-info">{{ statusLabel(d.status) }}</span>
          </div>

          <table class="table table-sm mt-3">
            <thead><tr><th>{{ '::AgainstVoucherType' | abpLocalization }}</th><th>{{ '::AgainstVoucherId' | abpLocalization }}</th><th>{{ 'Amount' | abpLocalization }}</th><th>{{ '::Unlinked' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (a of d.allocations; track a.id) {
                <tr>
                  <td>{{ a.againstVoucherType }}</td>
                  <td class="text-truncate" style="max-width:220px">{{ a.againstVoucherId }}</td>
                  <td>{{ a.amount }}</td>
                  <td>@if (a.unlinked) { <i class="fa fa-check text-success"></i> }</td>
                </tr>
              } @empty {
                <tr><td colspan="4" class="text-muted text-center">{{ '::NoAllocationsFound' | abpLocalization }}</td></tr>
              }
            </tbody>
          </table>

          @if (d.status === 0) {
            <div class="mt-3 d-flex gap-2">
              <button class="btn btn-sm btn-danger" [disabled]="!d.allocations?.length" (click)="submit()">
                <i class="fa fa-unlink me-1"></i>{{ '::Unreconcile' | abpLocalization }}
              </button>
            </div>
          }
        </div></div>
      }
    </abp-page>
  `,
})
export class UnreconcilePaymentDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(UnreconcilePaymentService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  d: UnreconcilePaymentDto | null = null;

  ngOnInit(): void { this.load(); }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: (r) => this.d = r, error: () => {} });
  }

  statusLabel(status: number | undefined): string { return STATUS_LABELS[status ?? 0] ?? 'Draft'; }
  voucherTypeLabel(type: number | undefined): string { return VOUCHER_TYPE_LABELS[type ?? 0] ?? 'Payment Entry'; }

  submit(): void {
    this.confirmation.warn('::UnreconcileConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === 'confirm') {
        const id = this.route.snapshot.paramMap.get('id')!;
        this.service.submit(id).subscribe({
          next: () => { this.toaster.success('::SuccessfullyUnreconciled'); this.load(); },
          error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
        });
      }
    });
  }
}
