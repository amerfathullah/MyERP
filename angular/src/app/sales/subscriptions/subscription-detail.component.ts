import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SubscriptionService, type SubscriptionDto } from '../../proxy/sales/sales-advanced.service';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-subscription-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'Subscriptions' | abpLocalization">
  <app-breadcrumb />
      @if (d) {
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-3"><strong>{{ 'SubscriptionNumber' | abpLocalization }}:</strong> {{ d.subscriptionNumber ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ 'Party' | abpLocalization }}:</strong> {{ d.partyName ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ 'BillingInterval' | abpLocalization }}:</strong> {{ d.billingInterval }}</div>
            <div class="col-md-3"><span class="badge" [ngClass]="statusClass(d.status)">{{ statusLabel(d.status) }}</span></div>
          </div>
          <div class="row mt-2">
            <div class="col-md-3"><strong>{{ 'StartDate' | abpLocalization }}:</strong> {{ d.startDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-3">@if (d.endDate) { <strong>{{ 'EndDate' | abpLocalization }}:</strong> {{ d.endDate | date:'dd/MM/yyyy' }} }</div>
            <div class="col-md-3"><strong>Current Period:</strong> {{ d.currentInvoiceStart | date:'dd/MM' }} – {{ d.currentInvoiceEnd | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-3"><strong>{{ 'Amount' | abpLocalization }}:</strong> <span class="fw-bold">{{ d.totalPerInterval | number:'1.2-2' }}</span></div>
          </div>
          @if (d.status === 0) {
            <div class="mt-3 d-flex gap-2">
              <button class="btn btn-sm btn-success" (click)="generateInvoice()"><i class="fa fa-file-invoice me-1"></i>{{ 'GenerateInvoice' | abpLocalization }}</button>
              <button class="btn btn-sm btn-primary" (click)="advancePeriod()"><i class="fa fa-forward me-1"></i>Advance Period</button>
              <button class="btn btn-sm btn-danger" (click)="cancel()"><i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            </div>
          }
        </div></div>
        @if ((d.plans ?? []).length > 0) {
          <div class="card"><div class="card-body">
            <h6>{{ 'Plans' | abpLocalization }}</h6>
            <table class="table table-sm">
              <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th class="text-end">{{ 'Quantity' | abpLocalization }}</th><th class="text-end">{{ 'Rate' | abpLocalization }}</th><th class="text-end">{{ 'Amount' | abpLocalization }}</th></tr></thead>
              <tbody>
                @for (p of d.plans; track p.id) {
                  <tr><td>{{ p.itemName ?? '—' }}</td><td class="text-end">{{ p.qty }}</td><td class="text-end">{{ p.rate | number:'1.2-2' }}</td><td class="text-end fw-bold">{{ ((p.qty ?? 0) * (p.rate ?? 0)) | number:'1.2-2' }}</td></tr>
                }
              </tbody>
            </table>
          </div></div>
        }
      }
    </abp-page>
  `,
})
export class SubscriptionDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(SubscriptionService);
  d: SubscriptionDto | null = null;

  ngOnInit() { this.load(); }

  load() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
  }

  advancePeriod() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.advancePeriod(id).subscribe(() => this.load());
  }

  cancel() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.cancel(id).subscribe(() => this.load());
  }

  generateInvoice() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.generateInvoice(id).subscribe({
      next: (result: any) => {
        this.load();
        alert(`Invoice ${result.invoiceNumber} created (${result.grandTotal})`);
      },
      error: () => {},
    });
  }

  statusLabel(s: number | undefined) { return ['Active', 'Past Due', 'Unpaid', 'Cancelled', 'Completed'][s ?? 0]; }
  statusClass(s: number | undefined) { return ['bg-success', 'bg-warning', 'bg-danger', 'bg-secondary', 'bg-info'][s ?? 0]; }
}
