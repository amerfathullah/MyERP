import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-batch-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <div class="container-fluid">
      <app-breadcrumb />
      @if (batch(); as b) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="mb-0">
              <i class="fas fa-layer-group me-2"></i>{{ b.batchNumber }}
              @if (b.disabled) {
                <span class="badge bg-danger ms-2">{{ 'Disabled' | abpLocalization }}</span>
              } @else if (isExpired()) {
                <span class="badge bg-warning text-dark ms-2">{{ 'Expired' | abpLocalization }}</span>
              } @else {
                <span class="badge bg-success ms-2">{{ 'Active' | abpLocalization }}</span>
              }
            </h5>
            @if (!b.disabled) {
              <button class="btn btn-outline-danger btn-sm" (click)="disable()">
                <i class="fas fa-ban me-1"></i>{{ 'Disable' | abpLocalization }}
              </button>
            }
          </div>
          <div class="card-body">
            <div class="row mb-4">
              <div class="col-md-3">
                <small class="text-muted">{{ 'Item' | abpLocalization }}</small>
                <div class="fw-bold">{{ b.itemName || b.itemId }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'ExpiryDate' | abpLocalization }}</small>
                <div>
                  @if (b.expiryDate) {
                    <span [class]="isExpired() ? 'text-danger fw-bold' : ''">
                      {{ b.expiryDate | date:'dd/MM/yyyy' }}
                    </span>
                    @if (daysUntilExpiry() !== null) {
                      <small class="ms-1" [class]="daysUntilExpiry()! < 0 ? 'text-danger' : daysUntilExpiry()! < 30 ? 'text-warning' : 'text-muted'">
                        ({{ daysUntilExpiry()! < 0 ? 'Expired ' + (-daysUntilExpiry()!) + ' days ago' : daysUntilExpiry() + ' days remaining' }})
                      </small>
                    }
                  } @else {
                    <span class="text-muted">{{ 'NoExpiry' | abpLocalization }}</span>
                  }
                </div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'ManufacturingDate' | abpLocalization }}</small>
                <div>{{ b.manufacturingDate ? (b.manufacturingDate | date:'dd/MM/yyyy') : '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'ShelfLifeDays' | abpLocalization }}</small>
                <div>{{ b.shelfLifeDays ?? '—' }}</div>
              </div>
            </div>
            <div class="row">
              <div class="col-md-3">
                <small class="text-muted">{{ 'Supplier' | abpLocalization }}</small>
                <div>{{ b.supplierId || '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'SupplierBatchNo' | abpLocalization }}</small>
                <div>{{ b.supplierBatchNo || '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'BatchWiseValuation' | abpLocalization }}</small>
                <div>
                  @if (b.useBatchwiseValuation) {
                    <span class="badge bg-info">{{ 'Enabled' | abpLocalization }}</span>
                  } @else {
                    <span class="badge bg-secondary">{{ 'Disabled' | abpLocalization }}</span>
                  }
                </div>
              </div>
            </div>
          </div>
        </div>

        <app-activity-log documentType="Batch" [documentId]="b.id" />
      } @else {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status"></div>
        </div>
      }
    </div>
  `
})
export class BatchDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private http = inject(HttpClient);
  private router = inject(Router);

  batch = signal<any>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.http.get<any>(`/api/app/batch/${id}`).subscribe({
      next: (data) => this.batch.set(data),
      error: () => this.router.navigate(['/inventory/batches'])
    });
  }

  isExpired(): boolean {
    const b = this.batch();
    if (!b?.expiryDate) return false;
    return new Date(b.expiryDate) < new Date();
  }

  daysUntilExpiry(): number | null {
    const b = this.batch();
    if (!b?.expiryDate) return null;
    const diff = new Date(b.expiryDate).getTime() - new Date().getTime();
    return Math.ceil(diff / (1000 * 60 * 60 * 24));
  }

  disable(): void {
    this.confirmation.warn('::DisableConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.http.post(`/api/app/batch/${this.batch()?.id}/disable`, {}).subscribe({
        next: () => {
          const b = this.batch();
          if (b) this.batch.set({ ...b, disabled: true });
        }
      });
    });
  }
}
