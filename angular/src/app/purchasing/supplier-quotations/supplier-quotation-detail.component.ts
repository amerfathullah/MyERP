import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-supplier-quotation-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent, StatusBadgeComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="sq?.quotationNumber ?? 'Supplier Quotation'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (sq) {
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Status' | abpLocalization }}</div>
              <app-status-badge [status]="sq.status" />
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Supplier' | abpLocalization }}</div>
              <div class="fw-bold">{{ sq.supplierName ?? sq.supplierId }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Date' | abpLocalization }}</div>
              <div>{{ sq.transactionDate | date:'dd/MM/yyyy' }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Total' | abpLocalization }}</div>
              <div class="fs-4 fw-bold text-primary">{{ sq.grandTotal | number:'1.2-2' }}</div>
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
                <th class="text-end">Amount</th>
              </tr></thead>
              <tbody>
                @for (item of sq.items; track $index) {
                  <tr>
                    <td>{{ item.itemName ?? item.itemId }}</td>
                    <td class="text-end">{{ item.quantity | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.rate | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold">{{ item.amount | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr class="fw-bold">
                  <td colspan="3" class="text-end">Grand Total</td>
                  <td class="text-end">{{ sq.grandTotal | number:'1.2-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `
})
export class SupplierQuotationDetailComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  sq: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.http.get<any>(`/api/app/supplier-quotation/${id}`).subscribe({
        next: s => { this.sq = s; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }
}
