import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ProformaInvoiceService } from '../../proxy/application/sales/proforma-invoice.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-proforma-invoice-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid">
      @if (loading()) {
        <div class="text-center py-5"><i class="fas fa-spinner fa-spin fa-2x"></i></div>
      } @else if (proforma()) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <div>
              <h5 class="mb-0">{{ proforma()!.proformaNumber }}</h5>
              <small class="text-muted">
                {{ 'ProformaInvoice' | abpLocalization }} •
                {{ proforma()!.proformaDate | date:'dd/MM/yyyy' }}
              </small>
            </div>
            <div class="d-flex gap-2 align-items-center">
              <app-status-badge [status]="getStatusLabel(proforma()!.status)" />
              @if (proforma()!.status === 1) {
                <button class="btn btn-sm btn-outline-danger" (click)="cancel()">
                  <i class="fas fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}
                </button>
              }
            </div>
          </div>
          <div class="card-body">
            <!-- Header Info -->
            <div class="row mb-4">
              <div class="col-md-3">
                <label class="form-label text-muted small mb-0">{{ 'Customer' | abpLocalization }}</label>
                <div class="fw-semibold">{{ proforma()!.customerName || '—' }}</div>
              </div>
              <div class="col-md-3">
                <label class="form-label text-muted small mb-0">{{ 'SalesOrder' | abpLocalization }}</label>
                <div><a [routerLink]="['/sales/orders', proforma()!.salesOrderId]">{{ proforma()!.salesOrderNumber || '—' }}</a></div>
              </div>
              <div class="col-md-2">
                <label class="form-label text-muted small mb-0">{{ 'Basis' | abpLocalization }}</label>
                <div><span class="badge" [class.bg-primary]="proforma()!.basedOn === 0" [class.bg-info]="proforma()!.basedOn === 1">
                  {{ proforma()!.basedOn === 0 ? 'Quantity' : 'Amount' }}
                </span></div>
              </div>
              <div class="col-md-2">
                <label class="form-label text-muted small mb-0">{{ 'Currency' | abpLocalization }}</label>
                <div>{{ proforma()!.currencyCode || 'MYR' }}</div>
              </div>
              <div class="col-md-2">
                <label class="form-label text-muted small mb-0">{{ 'GrandTotal' | abpLocalization }}</label>
                <div class="fw-bold text-primary fs-5">{{ proforma()!.grandTotal | number:'1.2-2' }}</div>
              </div>
            </div>

            <!-- Items Table -->
            <h6>{{ 'Items' | abpLocalization }}</h6>
            <div class="table-responsive">
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th>#</th>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    @if (!proforma()!.hideItemQty) {
                      <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                      <th>{{ 'UOM' | abpLocalization }}</th>
                      <th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                    }
                    <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of proforma()!.items; track item.id; let i = $index) {
                    <tr>
                      <td>{{ i + 1 }}</td>
                      <td>{{ item.itemName || item.itemCode }}</td>
                      @if (!proforma()!.hideItemQty) {
                        <td class="text-end">{{ item.quantity | number:'1.0-4' }}</td>
                        <td>{{ item.uom || '—' }}</td>
                        <td class="text-end">{{ item.rate | number:'1.2-4' }}</td>
                      }
                      <td class="text-end fw-semibold">{{ item.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="table-light">
                    <td [colSpan]="proforma()!.hideItemQty ? 1 : 4"></td>
                    <td class="text-end fw-bold">{{ 'GrandTotal' | abpLocalization }}</td>
                    <td class="text-end fw-bold text-primary">{{ proforma()!.grandTotal | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>

            <!-- Email Info -->
            @if (proforma()!.sentOn) {
              <div class="alert alert-info mt-3">
                <i class="fas fa-envelope me-2"></i>
                Emailed to {{ proforma()!.emailedTo }} on {{ proforma()!.sentOn | date:'dd/MM/yyyy HH:mm' }}
              </div>
            }
          </div>
        </div>

        <app-activity-log documentType="ProformaInvoice" [documentId]="proforma()!.id" />
      }
    </div>
  `
})
export class ProformaInvoiceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private proformaService = inject(ProformaInvoiceService);
  private toaster = inject(ToasterService);

  proforma = signal<any>(null);
  loading = signal(true);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.load(id);
  }

  load(id: string) {
    this.loading.set(true);
    this.proformaService.get(id).subscribe({
      next: (data) => { this.proforma.set(data); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  cancel() {
    if (!confirm('Cancel this proforma invoice?')) return;
    this.proformaService.cancel(this.proforma()!.id).subscribe({
      next: () => {
        this.toaster.success('Proforma Invoice cancelled');
        this.load(this.proforma()!.id);
      }
    });
  }

  getStatusLabel(status: number): string {
    return ['Draft', 'Issued', 'Cancelled'][status] ?? 'Unknown';
  }
}
