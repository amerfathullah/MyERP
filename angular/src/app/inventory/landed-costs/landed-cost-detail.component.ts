import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LandedCostVoucherService } from '../../proxy/inventory/landed-cost-voucher.service';
import type { LandedCostVoucherDto } from '../../proxy/dtos/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-lcv-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, ActivityLogComponent],
  template: `
    <abp-page [title]="'LandedCostVouchers' | abpLocalization">
      <app-breadcrumb />
      @if (d) {
        <div class="card mb-3"><div class="card-body">
          <div class="row align-items-center">
            <div class="col-md-3"><strong>{{ 'VoucherNumber' | abpLocalization }}:</strong> {{ d.voucherNumber ?? '—' }}</div>
            <div class="col-md-2"><strong>{{ 'Date' | abpLocalization }}:</strong> {{ d.postingDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-2"><strong>{{ 'TotalCharges' | abpLocalization }}:</strong> {{ d.totalCharges | number:'1.2-2' }}</div>
            <div class="col-md-2"><app-status-badge [status]="['Draft','Submitted','','','Cancelled'][d.status ?? 0]"></app-status-badge></div>
            <div class="col-md-3 text-end">
              @if ((d.status ?? 0) === 0) {
                <button class="btn btn-sm btn-primary" (click)="submit()" [disabled]="loading()"><i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}</button>
              }
              @if ((d.status ?? 0) === 1) {
                <button class="btn btn-sm btn-outline-danger" (click)="cancel()" [disabled]="loading()"><i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
              }
            </div>
          </div>
        </div></div>
        <div class="row mb-3">
          <div class="col-md-6"><div class="card"><div class="card-body">
            <h6>{{ 'Items' | abpLocalization }}</h6>
            <table class="table table-sm"><thead><tr><th>{{ 'Item' | abpLocalization }}</th><th class="text-end">{{ 'Amount' | abpLocalization }}</th><th class="text-end">{{ 'Charges' | abpLocalization }}</th></tr></thead>
            <tbody>@for (i of d.items ?? []; track i.id) { <tr><td>{{ i.description ?? '—' }}</td><td class="text-end">{{ i.amount | number:'1.2-2' }}</td><td class="text-end">{{ i.applicableCharges | number:'1.2-2' }}</td></tr> }</tbody></table>
          </div></div></div>
          <div class="col-md-6"><div class="card"><div class="card-body">
            <h6>{{ 'Charges' | abpLocalization }}</h6>
            <table class="table table-sm"><thead><tr><th>{{ 'Description' | abpLocalization }}</th><th class="text-end">{{ 'Amount' | abpLocalization }}</th></tr></thead>
            <tbody>@for (c of d.charges ?? []; track c.id) { <tr><td>{{ c.description }}</td><td class="text-end">{{ c.amount | number:'1.2-2' }}</td></tr> }</tbody></table>
          </div></div></div>
        </div>
        <app-activity-log documentType="LandedCostVoucher" [documentId]="d.id!" />
      }
    </abp-page>
  `,
})
export class LandedCostDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(LandedCostVoucherService);
  private toaster = inject(ToasterService);
  d: LandedCostVoucherDto | null = null;
  loading = signal(false);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
  }

  submit() {
    this.loading.set(true);
    this.service.submit(this.d!.id!).subscribe({
      next: () => { this.toaster.success('Landed Cost Voucher submitted'); this.reload(); },
      error: () => this.loading.set(false),
    });
  }

  cancel() {
    if (!confirm('Are you sure you want to cancel this voucher?')) return;
    this.loading.set(true);
    this.service.cancel(this.d!.id!).subscribe({
      next: () => { this.toaster.success('Landed Cost Voucher cancelled'); this.reload(); },
      error: () => this.loading.set(false),
    });
  }

  private reload() {
    this.loading.set(false);
    this.service.get(this.d!.id!).subscribe((r) => this.d = r);
  }
}
