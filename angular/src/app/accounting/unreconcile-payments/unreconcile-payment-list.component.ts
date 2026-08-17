import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { UnreconcilePaymentService } from '../../proxy/accounting/unreconcile-payment.service';
import type { UnreconcilePaymentDto } from '../../proxy/accounting/models';
import { CompanyContextService } from '../../shared/services/company-context.service';

const STATUS_LABELS = ['Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled'];
const VOUCHER_TYPE_LABELS = ['Payment Entry', 'Journal Entry'];

@Component({
  selector: 'app-unreconcile-payment-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::UnreconcilePayments' | abpLocalization">
      <div class="d-flex justify-content-end mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/accounting/unreconcile-payments/new">
          <i class="fa fa-plus me-1"></i>{{ '::New' | abpLocalization }}
        </button>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle">
          <thead>
            <tr>
              <th>{{ '::VoucherType' | abpLocalization }}</th>
              <th>{{ '::VoucherId' | abpLocalization }}</th>
              <th>{{ '::Allocations' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td>{{ voucherTypeLabel(item.voucherType) }}</td>
                <td class="text-truncate" style="max-width:220px">{{ item.voucherId }}</td>
                <td>{{ item.allocations?.length ?? 0 }}</td>
                <td><span class="badge bg-info">{{ statusLabel(item.status) }}</span></td>
                <td>
                  <a class="btn btn-sm btn-outline-primary" [routerLink]="'/accounting/unreconcile-payments/' + item.id"><i class="fa fa-eye"></i></a>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </abp-page>
  `,
})
export class UnreconcilePaymentListComponent implements OnInit {
  private service = inject(UnreconcilePaymentService);
  private companyContext = inject(CompanyContextService);

  items = signal<UnreconcilePaymentDto[]>([]);

  ngOnInit(): void {
    const companyId = this.companyContext.currentCompanyId();
    this.service.getList({ companyId: companyId ?? undefined, skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe({ next: (r) => this.items.set(r.items ?? []), error: () => {} });
  }

  statusLabel(status: number | undefined): string { return STATUS_LABELS[status ?? 0] ?? 'Draft'; }
  voucherTypeLabel(type: number | undefined): string { return VOUCHER_TYPE_LABELS[type ?? 0] ?? 'Payment Entry'; }
}
