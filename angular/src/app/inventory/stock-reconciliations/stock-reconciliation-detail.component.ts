import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StockReconciliationDetailService, type StockReconciliationDetailDto } from '../../shared/services/detail-services';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-stock-reconciliation-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, PageModule, LocalizationPipe, StatusBadgeComponent],
  template: `
    <abp-page [title]="'StockReconciliations' | abpLocalization">
  <app-breadcrumb />
      @if (d) {
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-3"><strong>{{ 'ReconciliationNumber' | abpLocalization }}:</strong> {{ d.reconciliationNumber ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ 'PostingDate' | abpLocalization }}:</strong> {{ d.postingDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-3"><strong>{{ 'DifferenceAmount' | abpLocalization }}:</strong> <span [class]="(d.differenceAmount ?? 0) < 0 ? 'text-danger' : 'text-success'">{{ d.differenceAmount | number:'1.2-2' }}</span></div>
            <div class="col-md-3"><app-status-badge [status]="['Draft','Submitted','','','Cancelled'][d.status ?? 0]"></app-status-badge></div>
          </div>
        </div></div>
        <div class="card"><div class="card-body">
          <table class="table table-sm">
            <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th class="text-end">{{ 'CurrentQty' | abpLocalization }}</th><th class="text-end">{{ 'NewQty' | abpLocalization }}</th><th class="text-end">{{ 'Difference' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (i of d.items ?? []; track i.id) {
                <tr><td>{{ i.itemId | slice:0:8 }}…</td><td class="text-end">{{ i.currentQuantity }}</td><td class="text-end">{{ i.newQuantity }}</td>
                <td class="text-end" [class]="(i.quantityDifference ?? 0) < 0 ? 'text-danger' : 'text-success'">{{ i.quantityDifference }}</td></tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `,
})
export class StockReconciliationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(StockReconciliationDetailService);
  d: StockReconciliationDetailDto | null = null;
  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
  }
}
