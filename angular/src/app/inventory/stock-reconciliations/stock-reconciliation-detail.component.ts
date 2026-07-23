import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockReconciliationService } from '../../proxy/inventory/stock-reconciliation.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { StockReconciliationDto } from '../../proxy/dtos/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-stock-reconciliation-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, PageModule, LocalizationPipe, StatusBadgeComponent, ActivityLogComponent],
  template: `
    <abp-page [title]="'StockReconciliations' | abpLocalization">
      <app-breadcrumb />
      @if (d) {
        <div class="card mb-3"><div class="card-body">
          <div class="row align-items-center">
            <div class="col-md-3"><strong>{{ 'ReconciliationNumber' | abpLocalization }}:</strong> {{ d.reconciliationNumber ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ 'PostingDate' | abpLocalization }}:</strong> {{ d.postingDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-2"><strong>{{ 'DifferenceAmount' | abpLocalization }}:</strong> <span [class]="(d.differenceAmount ?? 0) < 0 ? 'text-danger' : 'text-success'">{{ d.differenceAmount | number:'1.2-2' }}</span></div>
            <div class="col-md-2"><app-status-badge [status]="['Draft','Submitted','','','Cancelled'][d.status ?? 0]"></app-status-badge></div>
            <div class="col-md-2 text-end">
              @if ((d.status ?? 0) === 0) {
                <button class="btn btn-sm btn-primary" (click)="submit()" [disabled]="loading()"><i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}</button>
              }
              @if ((d.status ?? 0) === 1) {
                <button class="btn btn-sm btn-outline-danger" (click)="cancel()" [disabled]="loading()"><i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
              }
            </div>
          </div>
        </div></div>
        <div class="card mb-3"><div class="card-body">
          <table class="table table-sm">
            <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th class="text-end">{{ 'CurrentQty' | abpLocalization }}</th><th class="text-end">{{ 'NewQty' | abpLocalization }}</th><th class="text-end">{{ 'Difference' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (i of d.items ?? []; track i.id) {
                <tr><td>{{ itemNames()[i.itemId ?? ''] || (i.itemId | slice:0:8) + '…' }}</td><td class="text-end">{{ i.currentQuantity }}</td><td class="text-end">{{ i.newQuantity }}</td>
                <td class="text-end" [class]="(i.quantityDifference ?? 0) < 0 ? 'text-danger' : 'text-success'">{{ i.quantityDifference }}</td></tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-activity-log documentType="StockReconciliation" [documentId]="d.id!" />
      }
    </abp-page>
  `,
})
export class StockReconciliationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(StockReconciliationService);
  private itemService = inject(ItemService);
  private toaster = inject(ToasterService);
  d: StockReconciliationDto | null = null;
  loading = signal(false);
  itemNames = signal<Record<string, string>>({});

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(res => {
      const map: Record<string, string> = {};
      (res.items ?? []).forEach((i: any) => { map[i.id] = i.itemCode || i.itemName || i.id; });
      this.itemNames.set(map);
    });
  }

  submit() {
    this.loading.set(true);
    this.service.submit(this.d!.id!).subscribe({
      next: () => { this.toaster.success('Stock Reconciliation submitted'); this.reload(); },
      error: () => this.loading.set(false),
    });
  }

  cancel() {
    if (!confirm('Are you sure you want to cancel this reconciliation?')) return;
    this.loading.set(true);
    this.service.cancel(this.d!.id!).subscribe({
      next: () => { this.toaster.success('Stock Reconciliation cancelled'); this.reload(); },
      error: () => this.loading.set(false),
    });
  }

  private reload() {
    this.loading.set(false);
    this.service.get(this.d!.id!).subscribe((r) => this.d = r);
  }
}
