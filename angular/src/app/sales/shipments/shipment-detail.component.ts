import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-shipment-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <div class="container-fluid">
      <app-breadcrumb />
      @if (shipment(); as s) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="mb-0">
              <i class="fas fa-truck me-2"></i>{{ s.shipmentNumber }}
              @switch (s.status) {
                @case (0) { <span class="badge bg-secondary ms-2">Draft</span> }
                @case (1) { <span class="badge bg-primary ms-2">{{ 'Booked' | abpLocalization }}</span> }
                @case (2) { <span class="badge bg-info ms-2">{{ 'InTransit' | abpLocalization }}</span> }
                @case (3) { <span class="badge bg-success ms-2">{{ 'Delivered' | abpLocalization }}</span> }
                @case (4) { <span class="badge bg-dark ms-2">{{ 'Cancelled' | abpLocalization }}</span> }
              }
            </h5>
            <div class="btn-group btn-group-sm">
              @if (s.status === 0) {
                <button class="btn btn-outline-primary" (click)="submit()">
                  <i class="fas fa-check me-1"></i>{{ 'Book' | abpLocalization }}
                </button>
              }
              @if (s.status === 1) {
                <button class="btn btn-outline-info" (click)="markInTransit()">
                  <i class="fas fa-shipping-fast me-1"></i>{{ 'InTransit' | abpLocalization }}
                </button>
              }
              @if (s.status === 1 || s.status === 2) {
                <button class="btn btn-outline-success" (click)="markDelivered()">
                  <i class="fas fa-check-double me-1"></i>{{ 'MarkDelivered' | abpLocalization }}
                </button>
              }
              @if (s.status < 3) {
                <button class="btn btn-outline-danger" (click)="cancel()">
                  <i class="fas fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}
                </button>
              }
            </div>
          </div>
          <div class="card-body">
            <div class="row mb-4">
              <div class="col-md-3">
                <small class="text-muted">{{ 'PickupFrom' | abpLocalization }}</small>
                <div class="fw-bold">{{ s.pickupFromName || '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'DeliveryTo' | abpLocalization }}</small>
                <div class="fw-bold">{{ s.deliveryToName || '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'PickupDate' | abpLocalization }}</small>
                <div>{{ s.pickupDate ? (s.pickupDate | date:'dd/MM/yyyy') : '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'DeliveryDate' | abpLocalization }}</small>
                <div>{{ s.deliveryDate ? (s.deliveryDate | date:'dd/MM/yyyy') : '—' }}</div>
              </div>
            </div>

            <div class="row mb-4">
              <div class="col-md-3">
                <small class="text-muted">{{ 'Carrier' | abpLocalization }}</small>
                <div>{{ s.carrier || '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'TrackingNumber' | abpLocalization }}</small>
                <div>
                  @if (s.trackingUrl) {
                    <a [href]="s.trackingUrl" target="_blank" class="text-primary">
                      <i class="fas fa-external-link-alt me-1"></i>{{ s.trackingNumber }}
                    </a>
                  } @else {
                    {{ s.trackingNumber || '—' }}
                  }
                </div>
              </div>
              <div class="col-md-2">
                <small class="text-muted">{{ 'NetWeight' | abpLocalization }}</small>
                <div>{{ s.totalNetWeight ? (s.totalNetWeight | number:'1.2-2') : '—' }}</div>
              </div>
              <div class="col-md-2">
                <small class="text-muted">{{ 'ValueOfGoods' | abpLocalization }}</small>
                <div>{{ s.valueOfGoods ? (s.valueOfGoods | number:'1.2-2') : '—' }} {{ s.currencyCode }}</div>
              </div>
              <div class="col-md-2">
                <small class="text-muted">{{ 'DeliveryNotes' | abpLocalization }}</small>
                <div><span class="badge bg-info">{{ s.deliveryNoteCount }} {{ 'linked' | abpLocalization }}</span></div>
              </div>
            </div>

            @if (s.notes) {
              <div class="mb-3">
                <small class="text-muted">{{ 'Notes' | abpLocalization }}</small>
                <div>{{ s.notes }}</div>
              </div>
            }
          </div>
        </div>

        <app-activity-log documentType="Shipment" [documentId]="shipmentId" />
      } @else {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status"></div>
        </div>
      }
    </div>
  `
})
export class ShipmentDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private http = inject(HttpClient);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  shipment = signal<any>(null);
  shipmentId = '';

  ngOnInit(): void {
    this.shipmentId = this.route.snapshot.paramMap.get('id') ?? '';
    this.loadShipment();
  }

  private loadShipment(): void {
    this.http.get<any>(`/api/app/shipment/${this.shipmentId}`).subscribe({
      next: (data) => this.shipment.set(data),
      error: () => this.router.navigate(['/sales/shipments'])
    });
  }

  submit(): void {
    this.http.post<any>(`/api/app/shipment/${this.shipmentId}/submit`, {}).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.loadShipment(); }
    });
  }

  markInTransit(): void {
    this.http.post<any>(`/api/app/shipment/${this.shipmentId}/mark-in-transit`, {}).subscribe({
      next: () => { this.toaster.success('::MarkedInTransit'); this.loadShipment(); }
    });
  }

  markDelivered(): void {
    this.http.post<any>(`/api/app/shipment/${this.shipmentId}/mark-delivered`, {}).subscribe({
      next: () => { this.toaster.success('::MarkedDelivered'); this.loadShipment(); }
    });
  }

  cancel(): void {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.http.post<any>(`/api/app/shipment/${this.shipmentId}/cancel`, {}).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.loadShipment(); }
      });
    });
  }
}
