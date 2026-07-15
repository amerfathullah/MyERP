import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

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
                <th class="text-end">Qty</th>
                <th class="text-end">Rate</th>
                <th class="text-end">Ordered</th>
                <th class="text-end">Remaining</th>
              </tr></thead>
              <tbody>
                @for (item of order.items; track $index) {
                  <tr>
                    <td>{{ item.itemName ?? item.itemId }}</td>
                    <td class="text-end">{{ item.quantity | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.rate | number:'1.2-2' }}</td>
                    <td class="text-end">{{ item.orderedQty | number:'1.0-2' }}</td>
                    <td class="text-end fw-bold" [class.text-danger]="(item.quantity - item.orderedQty) <= 0">
                      {{ (item.quantity - (item.orderedQty ?? 0)) | number:'1.0-2' }}
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
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  order: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.http.get<any>(`/api/app/blanket-order/${id}`).subscribe({
        next: o => { this.order = o; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }
}
