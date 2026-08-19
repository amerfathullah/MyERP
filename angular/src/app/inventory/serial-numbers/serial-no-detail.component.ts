import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SerialNoService } from '../../proxy/inventory/serial-no.service';
import type { SerialNoDto } from '../../proxy/inventory/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-serial-no-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <div class="container-fluid">
      <app-breadcrumb />
      @if (serial(); as s) {
        <div class="card mb-3">
          <div class="card-header">
            <h5 class="mb-0">
              <i class="fas fa-barcode me-2"></i>{{ s.serialNumber }}
              @switch (s.maintenanceStatus) {
                @case ('UnderWarranty') { <span class="badge bg-success ms-2">Under Warranty</span> }
                @case ('UnderAmc') { <span class="badge bg-info ms-2">Under AMC</span> }
                @case ('OutOfWarranty') { <span class="badge bg-warning text-dark ms-2">Out of Warranty</span> }
                @case ('OutOfAmc') { <span class="badge bg-secondary ms-2">Out of AMC</span> }
              }
            </h5>
          </div>
          <div class="card-body">
            <div class="row mb-4">
              <div class="col-md-3">
                <small class="text-muted">{{ 'Item' | abpLocalization }}</small>
                <div class="fw-bold">{{ s.itemId }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'Warehouse' | abpLocalization }}</small>
                <div>{{ s.warehouseId || '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'PurchaseRate' | abpLocalization }}</small>
                <div>{{ s.purchaseRate ? (s.purchaseRate | number:'1.2-2') : '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'Status' | abpLocalization }}</small>
                <div>
                  @if (s.status === 0) {
                    <span class="badge bg-success">Active</span>
                  } @else {
                    <span class="badge bg-danger">Inactive</span>
                  }
                </div>
              </div>
            </div>
            <div class="row mb-4">
              <div class="col-md-3">
                <small class="text-muted">{{ 'WarrantyExpiryDate' | abpLocalization }}</small>
                <div>{{ s.warrantyExpiryDate ? (s.warrantyExpiryDate | date:'dd/MM/yyyy') : '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'AmcExpiryDate' | abpLocalization }}</small>
                <div>{{ s.amcExpiryDate ? (s.amcExpiryDate | date:'dd/MM/yyyy') : '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'Customer' | abpLocalization }}</small>
                <div>{{ s.customerId || '—' }}</div>
              </div>
            </div>
          </div>
        </div>

        <app-activity-log documentType="SerialNo" [documentId]="s.id!" />
      } @else {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status"></div>
        </div>
      }
    </div>
  `
})
export class SerialNoDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private serialNoService = inject(SerialNoService);
  private router = inject(Router);

  serial = signal<SerialNoDto | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.serialNoService.get(id).subscribe({
      next: (data: any) => this.serial.set(data),
      error: () => this.router.navigate(['/inventory/serial-numbers'])
    });
  }
}
