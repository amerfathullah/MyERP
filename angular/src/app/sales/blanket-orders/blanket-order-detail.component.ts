import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BlanketOrderService } from '../../proxy/sales/blanket-order.service';

@Component({
  selector: 'app-blanket-order-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent, StatusBadgeComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="order?.orderNumber ?? 'Blanket Order'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (order) {
        <div class="d-flex justify-content-between align-items-center mb-3">
          <div class="btn-group">
            @if (order.status === 0) {
              <button class="btn btn-primary" (click)="submit()">
                <i class="fa fa-check me-1"></i>{{ 'Submit' | abpLocalization }}
              </button>
            }
            @if (order.status === 1) {
              <button class="btn btn-warning" (click)="close()">
                <i class="fa fa-lock me-1"></i>{{ 'Close' | abpLocalization }}
              </button>
              <button class="btn btn-outline-danger" (click)="cancel()">
                <i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}
              </button>
            }
            @if (order.status === 14) {
              <button class="btn btn-info text-white" (click)="reopen()">
                <i class="fa fa-unlock me-1"></i>Reopen
              </button>
            }
          </div>
        </div>

        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Status' | abpLocalization }}</div>
              <app-status-badge [status]="order.status" />
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Order Type</div>
              <div class="fs-5 fw-bold">{{ order.orderType }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'From' | abpLocalization }}</div>
              <div>{{ order.fromDate | date:'dd/MM/yyyy' }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'To' | abpLocalization }}</div>
              <div>{{ order.toDate | date:'dd/MM/yyyy' }}</div>
            </div></div>
          </div>
        </div>

        <div class="card"><div class="card-header"><h6 class="mb-0">{{ 'Items' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>Item</th>
                <th>UoM</th>
                <th class="text-end">Qty</th>
                <th class="text-end">Rate</th>
                <th class="text-end">Ordered</th>
                <th class="text-end">Remaining</th>
                <th class="text-center">Status</th>
                <th class="text-end">Actions</th>
              </tr></thead>
              <tbody>
                @for (item of order.items; track item.id ?? $index) {
                  <tr [class.table-light]="item.isClosed">
                    <td>{{ item.itemName ?? item.itemId }}</td>
                    <td>{{ item.stockUom || '—' }}</td>
                    <td class="text-end">{{ item.qty | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.rate | number:'1.2-2' }}</td>
                    <td class="text-end">{{ item.orderedQty | number:'1.0-2' }}</td>
                    <td class="text-end fw-bold" [class.text-danger]="(item.remainingQty ?? (item.qty - (item.orderedQty ?? 0))) <= 0">
                      {{ (item.remainingQty ?? (item.qty - (item.orderedQty ?? 0))) | number:'1.0-2' }}
                    </td>
                    <td class="text-center">
                      @if (item.isClosed) {
                        <span class="badge bg-secondary">Closed</span>
                      } @else {
                        <span class="badge bg-success">Open</span>
                      }
                    </td>
                    <td class="text-end">
                      @if (!item.isClosed && order.status === 1) {
                        <button class="btn btn-sm btn-outline-warning" (click)="closeItem(item.id)">
                          <i class="fa fa-lock me-1"></i>Close
                        </button>
                      }
                      @if (item.isClosed && (order.status === 1 || order.status === 14)) {
                        <button class="btn btn-sm btn-outline-info" (click)="reopenItem(item.id)">
                          <i class="fa fa-unlock me-1"></i>Reopen
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `
})
export class BlanketOrderDetailComponent implements OnInit {
  private service = inject(BlanketOrderService);
  private route = inject(ActivatedRoute);
  order: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.service.get(id).subscribe({
        next: o => { this.order = o; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }

  submit() {
    if (!this.order?.id) return;
    this.service.submit(this.order.id).subscribe(o => this.order = o);
  }

  close() {
    if (!this.order?.id) return;
    this.service.close(this.order.id).subscribe(o => this.order = o);
  }

  reopen() {
    if (!this.order?.id) return;
    this.service.reopen(this.order.id).subscribe(o => this.order = o);
  }

  cancel() {
    if (!this.order?.id) return;
    this.service.cancel(this.order.id).subscribe(o => this.order = o);
  }

  closeItem(itemId: string) {
    if (!this.order?.id || !itemId) return;
    this.service.closeItem(this.order.id, itemId).subscribe(o => this.order = o);
  }

  reopenItem(itemId: string) {
    if (!this.order?.id || !itemId) return;
    this.service.reopenItem(this.order.id, itemId).subscribe(o => this.order = o);
  }
}
