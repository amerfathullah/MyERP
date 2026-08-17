import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { PaymentOrderService } from '../../proxy/accounting/payment-order.service';
import type { PaymentOrderDto } from '../../proxy/accounting/models';
import { CompanyContextService } from '../../shared/services/company-context.service';

const STATUS_LABELS = ['Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled'];

@Component({
  selector: 'app-payment-order-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::PaymentOrders' | abpLocalization">
      <div class="d-flex justify-content-end mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/accounting/payment-orders/new">
          <i class="fa fa-plus me-1"></i>{{ '::New' | abpLocalization }}
        </button>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle">
          <thead>
            <tr>
              <th>{{ '::OrderNumber' | abpLocalization }}</th>
              <th>{{ '::PostingDate' | abpLocalization }}</th>
              <th>{{ '::Type' | abpLocalization }}</th>
              <th>{{ '::References' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td>{{ item.orderNumber }}</td>
                <td>{{ item.postingDate | date:'dd/MM/yyyy' }}</td>
                <td>{{ item.paymentOrderType === 0 ? ('::PaymentRequest' | abpLocalization) : ('::PaymentEntry' | abpLocalization) }}</td>
                <td>{{ item.references?.length ?? 0 }}</td>
                <td><span class="badge bg-info">{{ statusLabel(item.status) }}</span></td>
                <td>
                  <a class="btn btn-sm btn-outline-primary" [routerLink]="'/accounting/payment-orders/' + item.id"><i class="fa fa-eye"></i></a>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </abp-page>
  `,
})
export class PaymentOrderListComponent implements OnInit {
  private service = inject(PaymentOrderService);
  private companyContext = inject(CompanyContextService);

  items = signal<PaymentOrderDto[]>([]);

  ngOnInit(): void {
    const companyId = this.companyContext.currentCompanyId();
    this.service.getList({ companyId: companyId ?? undefined, skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe({ next: (r) => this.items.set(r.items ?? []), error: () => {} });
  }

  statusLabel(status: number | undefined): string {
    return STATUS_LABELS[status ?? 0] ?? 'Draft';
  }
}
