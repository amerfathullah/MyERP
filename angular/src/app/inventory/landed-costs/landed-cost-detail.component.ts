import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LandedCostDetailService, type LandedCostVoucherDetailDto } from '../../proxy/detail-services';

@Component({
  selector: 'app-lcv-detail', standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent],
  template: `
    <abp-page [title]="'LandedCostVouchers' | abpLocalization">
      @if (d) {
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-3"><strong>{{ 'VoucherNumber' | abpLocalization }}:</strong> {{ d.voucherNumber ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ 'Date' | abpLocalization }}:</strong> {{ d.postingDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-3"><strong>{{ 'TotalCharges' | abpLocalization }}:</strong> {{ d.totalCharges | number:'1.2-2' }}</div>
            <div class="col-md-3"><app-status-badge [status]="['Draft','Submitted','','','Cancelled'][d.status]"></app-status-badge></div>
          </div>
        </div></div>
        <div class="row">
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
      }
    </abp-page>
  `,
})
export class LandedCostDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(LandedCostDetailService);
  d: LandedCostVoucherDetailDto | null = null;
  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
  }
}
