import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { SupplierQuotationService, type SupplierQuotationDto } from '../../proxy/sales/additional-proxies.service';

@Component({
  selector: 'app-supplier-quotation-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'SupplierQuotations' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/purchasing/supplier-quotations/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewSupplierQuotation' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && quotations.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-file-invoice-dollar fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSupplierQuotationsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'QuotationNumber' | abpLocalization }}</th>
              <th>{{ 'Supplier' | abpLocalization }}</th>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th>{{ 'ValidTill' | abpLocalization }}</th>
              <th class="text-end">{{ 'GrandTotal' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (sq of quotations; track sq.id) {
                <tr>
                  <td>{{ sq.quotationNumber ?? '—' }}</td>
                  <td>{{ sq.supplierName ?? '—' }}</td>
                  <td>{{ sq.transactionDate | date:'dd/MM/yyyy' }}</td>
                  <td>{{ sq.validTill ? (sq.validTill | date:'dd/MM/yyyy') : '—' }}</td>
                  <td class="text-end fw-bold">{{ sq.grandTotal | number:'1.2-2' }}</td>
                  <td><span class="badge" [ngClass]="{'bg-secondary': sq.status===0, 'bg-success': sq.status===1, 'bg-danger': sq.status===4}">
                    {{ ['Draft','Submitted','','','Cancelled'][sq.status] }}
                  </span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `,
})
export class SupplierQuotationListComponent implements OnInit {
  private service = inject(SupplierQuotationService);
  quotations: SupplierQuotationDto[] = [];
  isLoading = false;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 50 })
      .subscribe({ next: (r) => { this.quotations = r.items ?? []; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }
}
