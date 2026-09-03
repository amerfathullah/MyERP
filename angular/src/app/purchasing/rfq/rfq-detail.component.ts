import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { RequestForQuotationService } from '../../proxy/purchasing/request-for-quotation.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';

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

        @if (rfq.status === 'Submitted' || rfq.status === 1) {
          <div class="d-flex flex-wrap gap-2 mb-3">
            <button class="btn btn-outline-info btn-sm" (click)="viewSupplierQuotationComparison()">
              <i class="fa fa-balance-scale me-1"></i>Supplier Quotation Comparison
            </button>
            @for (s of rfq.suppliers; track s.supplierId) {
              <button class="btn btn-outline-primary btn-sm" (click)="createSupplierQuotation(s.supplierId, s.supplierName)">
                <i class="fa fa-file-invoice me-1"></i>Create SQ for {{ s.supplierName }}
              </button>
            }
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
  private service = inject(RequestForQuotationService);
  private conversionService = inject(PurchaseConversionService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  rfq: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.service.get(id).subscribe({
        next: r => { this.rfq = r; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }

  viewSupplierQuotationComparison() {
    if (!this.rfq?.id) return;
    this.router.navigate(['/purchasing/supplier-quotation-comparison'], {
      queryParams: { rfqId: this.rfq.id }
    });
  }

  createSupplierQuotation(supplierId: string, supplierName: string) {
    this.confirmation.warn('::CreateConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.isLoading = true;
      this.conversionService.convertRfqToSupplierQuotation(this.rfq.id, supplierId).subscribe({
        next: (sq) => {
          this.isLoading = false;
          this.toaster.success('::SuccessfullyCreated');
          this.router.navigate(['/purchasing/supplier-quotations', (sq as any).id]);
        },
        error: (err) => {
          this.isLoading = false;
          this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        },
      });
    });
  }
}
