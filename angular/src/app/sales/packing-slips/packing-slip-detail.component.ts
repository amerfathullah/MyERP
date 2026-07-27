import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { PackingSlipService } from '../../proxy/sales/packing-slip.service';

@Component({
  selector: 'app-packing-slip-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="'PackingSlip' | abpLocalization">
      @if (isLoading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (slip(); as s) {
        <!-- KPI Cards -->
        <div class="row mb-3">
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
              <app-status-badge [status]="s.status" />
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'CaseNumbers' | abpLocalization }}</small>
              <h5 class="mb-0">{{ s.fromCaseNo }} — {{ s.toCaseNo }}</h5>
              <small class="text-muted">{{ s.numberOfCases || ((s.toCaseNo || 0) - (s.fromCaseNo || 0) + 1) }} {{ 'Cases' | abpLocalization }}</small>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'GrossWeight' | abpLocalization }}</small>
              <h5 class="mb-0">{{ s.grossWeightKg | number:'1.2-2' }} {{ s.weightUom || 'Kg' }}</h5>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'NetWeight' | abpLocalization }}</small>
              <h5 class="mb-0">{{ s.netWeightKg | number:'1.2-2' }} {{ s.weightUom || 'Kg' }}</h5>
            </div></div>
          </div>
        </div>

        <!-- Delivery Note Reference -->
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'Reference' | abpLocalization }}</h6>
          <table class="table table-borderless table-sm mb-0">
            <tr><td class="text-muted" style="width:30%">{{ 'DeliveryNote' | abpLocalization }}</td>
              <td>
                @if (s.deliveryNoteId) {
                  <a [routerLink]="['/sales/delivery-notes', s.deliveryNoteId]">{{ s.deliveryNoteNumber || s.deliveryNoteId }}</a>
                } @else { <span>—</span> }
              </td>
            </tr>
            <tr><td class="text-muted">{{ 'Created' | abpLocalization }}</td><td>{{ s.creationTime | date:'dd/MM/yyyy HH:mm' }}</td></tr>
          </table>
        </div></div>

        <!-- Items Table -->
        @if (s.items?.length) {
          <div class="card mb-3"><div class="card-body">
            <h6>{{ 'Items' | abpLocalization }}</h6>
            <table class="table table-hover table-sm mb-0">
              <thead><tr>
                <th>#</th>
                <th>{{ 'Item' | abpLocalization }}</th>
                <th>{{ 'Description' | abpLocalization }}</th>
                <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                <th class="text-end">{{ 'NetWeight' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (item of s.items; track item.id; let i = $index) {
                  <tr>
                    <td>{{ i + 1 }}</td>
                    <td><strong>{{ item.itemCode || item.itemName || '—' }}</strong></td>
                    <td>{{ item.description || '—' }}</td>
                    <td class="text-end">{{ item.qty | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.netWeight | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div></div>
        }

        <!-- Workflow Actions -->
        <div class="d-flex gap-2 mb-3">
          @if (s.status === 0) {
            <button class="btn btn-primary btn-sm" (click)="submit()">
              <i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}
            </button>
            <button class="btn btn-outline-danger btn-sm" (click)="deleteSlip()">
              <i class="fa fa-trash me-1"></i>{{ 'Delete' | abpLocalization }}
            </button>
          }
          @if (s.status === 1) {
            <button class="btn btn-outline-danger btn-sm" (click)="cancelSlip()">
              <i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}
            </button>
          }
          <button class="btn btn-outline-secondary btn-sm" (click)="print()">
            <i class="fa fa-print me-1"></i>{{ 'Print' | abpLocalization }}
          </button>
        </div>

        <!-- Activity Log -->
        <app-activity-log documentType="PackingSlip" [documentId]="slipId" />
      }
    </abp-page>
  `
})
export class PackingSlipDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private packingSlipService = inject(PackingSlipService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);

  slip = signal<any>(null);
  isLoading = signal(true);
  slipId = '';

  ngOnInit() {
    this.slipId = this.route.snapshot.paramMap.get('id') ?? '';
    this.loadSlip();
  }

  loadSlip() {
    this.isLoading.set(true);
    this.packingSlipService.get(this.slipId).subscribe({
      next: s => { this.slip.set(s); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }

  submit() {
    this.packingSlipService.submit(this.slipId).subscribe({
      next: () => { this.toaster.success(this.l('::SuccessfullySubmitted')); this.loadSlip(); },
      error: () => {}
    });
  }

  cancelSlip() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.packingSlipService.cancel(this.slipId).subscribe({
        next: () => { this.toaster.success(this.l('::SuccessfullyCancelled')); this.loadSlip(); },
        error: () => {}
      });
    });
  }

  deleteSlip() {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.packingSlipService.delete(this.slipId).subscribe({
        next: () => { this.toaster.success(this.l('::SuccessfullyDeleted')); history.back(); },
        error: () => {}
      });
    });
  }

  print() { window.print(); }

  private l(key: string): string { return this.localization.instant(key); }
}
