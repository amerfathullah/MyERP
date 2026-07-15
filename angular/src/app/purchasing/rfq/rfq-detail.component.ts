import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-rfq-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent, StatusBadgeComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="rfq?.rfqNumber ?? 'Request for Quotation'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (rfq) {
        <div class="row g-3 mb-4">
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Status' | abpLocalization }}</div>
              <app-status-badge [status]="rfq.status" />
            </div></div>
          </div>
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Items' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ rfq.items?.length ?? 0 }}</div>
            </div></div>
          </div>
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Suppliers</div>
              <div class="fs-4 fw-bold">{{ rfq.suppliers?.length ?? 0 }}</div>
            </div></div>
          </div>
        </div>

        @if (rfq.messageForSupplier) {
          <div class="alert alert-light">
            <strong>Message for Suppliers:</strong><br>{{ rfq.messageForSupplier }}
          </div>
        }

        <div class="card mb-4"><div class="card-header"><h6 class="mb-0">{{ 'Items' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr><th>Item</th><th class="text-end">Qty</th><th>Required By</th></tr></thead>
              <tbody>
                @for (item of rfq.items; track $index) {
                  <tr>
                    <td>{{ item.itemName ?? item.itemId }}</td>
                    <td class="text-end">{{ item.quantity | number:'1.0-2' }}</td>
                    <td>{{ item.requiredDate | date:'dd/MM/yyyy' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <div class="card"><div class="card-header"><h6 class="mb-0">Suppliers</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr><th>Supplier</th><th>Quote Status</th><th>Email Sent</th></tr></thead>
              <tbody>
                @for (s of rfq.suppliers; track $index) {
                  <tr>
                    <td>{{ s.supplierName ?? s.supplierId }}</td>
                    <td>
                      <span class="badge" [class]="s.quoteStatus === 'Received' ? 'bg-success' : 'bg-warning'">
                        {{ s.quoteStatus ?? 'Pending' }}
                      </span>
                    </td>
                    <td>@if (s.emailSent) { <i class="fa fa-check text-success"></i> } @else { <i class="fa fa-minus text-muted"></i> }</td>
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
export class RfqDetailComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  rfq: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.http.get<any>(`/api/app/request-for-quotation/${id}`).subscribe({
        next: r => { this.rfq = r; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }
}
