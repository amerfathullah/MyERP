import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import { AssetCapitalizationService } from '../../proxy/assets/asset-capitalization.service';
import type { AssetCapitalizationDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-capitalization-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent, VoucherLedgerComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid">
      @if (loading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (cap()) {
        <!-- Header -->
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <div>
              <h5 class="mb-0">{{ cap()!.targetAssetName || ('AssetCapitalization' | abpLocalization) }}</h5>
              <small class="text-muted">
                {{ 'AssetCapitalization' | abpLocalization }} &bull;
                {{ cap()!.postingDate | date:'dd MMM yyyy' }}
              </small>
            </div>
            <div class="d-flex gap-2 align-items-center">
              <app-status-badge [status]="getStatusLabel(cap()!.status)" />
              @if (cap()!.status === 0) {
                <button class="btn btn-sm btn-primary" (click)="submit()">
                  <i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}
                </button>
              }
              @if (cap()!.status === 1) {
                <button class="btn btn-sm btn-outline-danger" (click)="cancel()">
                  <i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}
                </button>
              }
            </div>
          </div>
          <div class="card-body">
            <div class="row">
              <div class="col-md-4">
                <label class="form-label text-muted small mb-0">{{ 'TargetAsset' | abpLocalization }}</label>
                <div class="fw-semibold">
                  @if (cap()!.targetAssetId) {
                    <a [routerLink]="['/assets', cap()!.targetAssetId]">{{ cap()!.targetAssetName }}</a>
                  } @else {
                    {{ cap()!.targetAssetName || '—' }}
                  }
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label text-muted small mb-0">{{ 'PostingDate' | abpLocalization }}</label>
                <div>{{ cap()!.postingDate | date:'dd MMM yyyy' }}</div>
              </div>
              <div class="col-md-4">
                <label class="form-label text-muted small mb-0">{{ 'TotalAssetValue' | abpLocalization }}</label>
                <div class="fw-bold text-primary fs-5">{{ cap()!.totalCapitalizedAmount | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Stock Items -->
        @if (cap()!.stockItems?.length) {
          <div class="card mb-3">
            <div class="card-header">
              <h6 class="card-title mb-0"><i class="fa fa-boxes-stacked me-2"></i>{{ 'StockItems' | abpLocalization }}</h6>
            </div>
            <div class="card-body p-0">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>#</th>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of cap()!.stockItems; track $index; let i = $index) {
                    <tr>
                      <td>{{ i + 1 }}</td>
                      <td>{{ item.itemName || item.itemId }}</td>
                      <td class="text-end">{{ item.qty | number:'1.0-4' }}</td>
                      <td class="text-end">{{ item.rate | number:'1.2-4' }}</td>
                      <td class="text-end fw-semibold">{{ (item.qty ?? 0) * (item.rate ?? 0) | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light">
                  <tr>
                    <td colspan="4" class="text-end fw-bold">{{ 'Total' | abpLocalization }}</td>
                    <td class="text-end fw-bold">{{ stockItemsTotal | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        }

        <!-- Service Items -->
        @if (cap()!.serviceItems?.length) {
          <div class="card mb-3">
            <div class="card-header">
              <h6 class="card-title mb-0"><i class="fa fa-wrench me-2"></i>{{ 'ServiceItems' | abpLocalization }}</h6>
            </div>
            <div class="card-body p-0">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>#</th>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of cap()!.serviceItems; track $index; let i = $index) {
                    <tr>
                      <td>{{ i + 1 }}</td>
                      <td>{{ item.itemName || item.itemId }}</td>
                      <td class="text-end fw-semibold">{{ item.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light">
                  <tr>
                    <td class="text-end fw-bold">{{ 'Total' | abpLocalization }}</td>
                    <td class="text-end fw-bold">{{ serviceItemsTotal | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        }

        <!-- Consumed Assets -->
        @if (cap()!.consumedAssets?.length) {
          <div class="card mb-3">
            <div class="card-header">
              <h6 class="card-title mb-0"><i class="fa fa-building me-2"></i>{{ 'ConsumedAssets' | abpLocalization }}</h6>
            </div>
            <div class="card-body p-0">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>#</th>
                    <th>{{ 'AssetName' | abpLocalization }}</th>
                    <th class="text-end">{{ 'ValueAfterDepreciation' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (asset of cap()!.consumedAssets; track $index; let i = $index) {
                    <tr>
                      <td>{{ i + 1 }}</td>
                      <td>
                        @if (asset.assetId) {
                          <a [routerLink]="['/assets', asset.assetId]">{{ asset.assetName }}</a>
                        } @else {
                          {{ asset.assetName || '—' }}
                        }
                      </td>
                      <td class="text-end fw-semibold">{{ asset.currentValue | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light">
                  <tr>
                    <td class="text-end fw-bold">{{ 'Total' | abpLocalization }}</td>
                    <td class="text-end fw-bold">{{ consumedAssetsTotal | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        }

        <!-- No child items fallback -->
        @if (!cap()!.stockItems?.length && !cap()!.serviceItems?.length && !cap()!.consumedAssets?.length) {
          <div class="card mb-3">
            <div class="card-body text-center text-muted py-4">
              <i class="fa fa-info-circle me-2"></i>{{ 'CapitalizationValueSummary' | abpLocalization }}:
              <span class="fw-bold text-primary ms-1">{{ cap()!.totalCapitalizedAmount | number:'1.2-2' }}</span>
            </div>
          </div>
        }

        <app-activity-log documentType="AssetCapitalization" [documentId]="cap()!.id!" />
        <app-voucher-ledger voucherType="AssetCapitalization" [voucherId]="cap()!.id!" [showStock]="true" [showAccounting]="true" />
      }
    </div>
  `
})
export class AssetCapitalizationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(AssetCapitalizationService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  cap = signal<AssetCapitalizationDto | null>(null);
  loading = signal(true);

  get stockItemsTotal(): number {
    return (this.cap()?.stockItems ?? []).reduce((sum, i) => sum + (i.qty ?? 0) * (i.rate ?? 0), 0);
  }

  get serviceItemsTotal(): number {
    return (this.cap()?.serviceItems ?? []).reduce((sum, i) => sum + (i.amount ?? 0), 0);
  }

  get consumedAssetsTotal(): number {
    return (this.cap()?.consumedAssets ?? []).reduce((sum, i) => sum + (i.currentValue ?? 0), 0);
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.load(id);
  }

  load(id: string) {
    this.loading.set(true);
    this.service.get(id).subscribe({
      next: (data) => { this.cap.set(data); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  submit() {
    this.confirmation.warn('::SubmitCapitalizationConfirmation', '::AreYouSure').subscribe(status => {
      if (status === Confirmation.Status.confirm) {
        this.service.submit(this.cap()!.id).subscribe({
          next: () => {
            this.toaster.success('::CapitalizationSubmitted');
            this.load(this.cap()!.id);
          }
        });
      }
    });
  }

  cancel() {
    this.confirmation.warn('::CancelCapitalizationConfirmation', '::AreYouSure').subscribe(status => {
      if (status === Confirmation.Status.confirm) {
        this.service.cancel(this.cap()!.id).subscribe({
          next: () => {
            this.toaster.success('::CapitalizationCancelled');
            this.load(this.cap()!.id);
          }
        });
      }
    });
  }
}
