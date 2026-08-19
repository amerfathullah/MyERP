import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { WarrantyClaimService } from '../../proxy/maintenance/warranty-claim.service';
import type { WarrantyClaimDto } from '../../proxy/maintenance/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-warranty-claim-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="claim()?.claimNumber || ('WarrantyClaim' | abpLocalization)">
      @if (isLoading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (claim(); as c) {
        <!-- KPI Cards -->
        <div class="row mb-3">
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
              <span class="badge mt-1" [class]="getStatusClass(c.status ?? 0)">{{ getStatusName(c.status ?? 0) }}</span>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'ComplaintDate' | abpLocalization }}</small>
              <strong>{{ c.complaintDate | date:'dd/MM/yyyy' }}</strong>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'Warranty' | abpLocalization }}</small>
              <span class="badge mt-1" [class]="c.isUnderWarranty ? 'bg-success' : 'bg-warning text-dark'">
                {{ c.isUnderWarranty ? ('::UnderWarranty' | abpLocalization) : ('::Expired' | abpLocalization) }}
              </span>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'Resolution' | abpLocalization }}</small>
              <strong>{{ c.resolutionDate ? (c.resolutionDate | date:'dd/MM/yyyy') : '—' }}</strong>
            </div></div>
          </div>
        </div>

        <!-- Details Card -->
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-6">
              <table class="table table-borderless table-sm mb-0">
                <tr><td class="text-muted" style="width:40%">{{ 'Customer' | abpLocalization }}</td><td><strong>{{ c.customerName || '—' }}</strong></td></tr>
                <tr><td class="text-muted">{{ 'Item' | abpLocalization }}</td><td>{{ c.itemName || '—' }}</td></tr>
                <tr><td class="text-muted">{{ 'SerialNo' | abpLocalization }}</td><td>
                  @if (c.serialNoId) { <a [routerLink]="['/inventory/serial-numbers', c.serialNoId]">{{ c.serialNoId }}</a> } @else { — }
                </td></tr>
                <tr><td class="text-muted">{{ 'SalesInvoice' | abpLocalization }}</td><td>
                  @if (c.salesInvoiceId) { <a [routerLink]="['/sales/invoices', c.salesInvoiceId]">{{ c.salesInvoiceId }}</a> } @else { — }
                </td></tr>
              </table>
            </div>
            <div class="col-md-6">
              <table class="table table-borderless table-sm mb-0">
                <tr><td class="text-muted" style="width:40%">{{ 'WarrantyExpiryDate' | abpLocalization }}</td><td>{{ c.warrantyExpiryDate ? (c.warrantyExpiryDate | date:'dd/MM/yyyy') : '—' }}</td></tr>
                <tr><td class="text-muted">{{ 'AmcExpiryDate' | abpLocalization }}</td><td>{{ c.amcExpiryDate ? (c.amcExpiryDate | date:'dd/MM/yyyy') : '—' }}</td></tr>
              </table>
            </div>
          </div>
        </div></div>

        <!-- Complaint -->
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'Complaint' | abpLocalization }}</h6>
          <p class="mb-0">{{ c.complaint || '—' }}</p>
        </div></div>

        <!-- Resolution (if resolved) -->
        @if (c.resolution) {
          <div class="card mb-3"><div class="card-body">
            <h6>{{ 'Resolution' | abpLocalization }}</h6>
            <p class="mb-0">{{ c.resolution }}</p>
          </div></div>
        }

        <!-- Workflow Actions -->
        <div class="d-flex gap-2 mb-3">
          @if (c.status === 0) {
            <button class="btn btn-primary btn-sm" (click)="startWork()">
              <i class="fa fa-wrench me-1"></i>{{ 'StartWork' | abpLocalization }}
            </button>
          }
          @if (c.status === 0 || c.status === 1) {
            <button class="btn btn-success btn-sm" (click)="closePrompt()">
              <i class="fa fa-check me-1"></i>{{ 'Close' | abpLocalization }}
            </button>
            <button class="btn btn-outline-danger btn-sm" (click)="cancelClaim()">
              <i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}
            </button>
          }
        </div>

        <!-- Resolution Prompt -->
        @if (showResolutionPrompt()) {
          <div class="card mb-3 border-success"><div class="card-body">
            <h6>{{ 'Resolution' | abpLocalization }}</h6>
            <textarea class="form-control mb-2" rows="3" #resolutionInput
              [placeholder]="'Placeholder:Resolution' | abpLocalization"></textarea>
            <button class="btn btn-success btn-sm me-2" (click)="closeClaim(resolutionInput.value)">
              {{ 'Confirm' | abpLocalization }}
            </button>
            <button class="btn btn-outline-secondary btn-sm" (click)="showResolutionPrompt.set(false)">
              {{ 'Cancel' | abpLocalization }}
            </button>
          </div></div>
        }

        <!-- Activity Log -->
        <app-activity-log documentType="WarrantyClaim" [documentId]="claimId" />
      }
    </abp-page>
  `
})
export class WarrantyClaimDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private router = inject(Router);
  private claimService = inject(WarrantyClaimService);
  private toaster = inject(ToasterService);

  private localization = inject(LocalizationService);

  claim = signal<WarrantyClaimDto | null>(null);
  isLoading = signal(true);
  showResolutionPrompt = signal(false);
  claimId = '';

  private statusNames = ['Open', 'Work In Progress', 'Closed', 'Cancelled'];
  private statusClasses = ['bg-primary', 'bg-info', 'bg-success', 'bg-secondary'];

  ngOnInit() {
    this.claimId = this.route.snapshot.paramMap.get('id') ?? '';
    this.loadClaim();
  }

  loadClaim() {
    this.isLoading.set(true);
    this.claimService.get(this.claimId).subscribe({
      next: (c: any) => { this.claim.set(c); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); this.toaster.error(this.l('::FailedToLoad')); }
    });
  }

  getStatusName(s: number) { return this.statusNames[s] ?? 'Unknown'; }
  getStatusClass(s: number) { return this.statusClasses[s] ?? 'bg-secondary'; }

  startWork() {
    this.claimService.startWork(this.claimId).subscribe({
      next: () => { this.toaster.success(this.l('::WorkStarted')); this.loadClaim(); },
      error: (err) => this.toaster.error(err?.error?.error?.message || this.l('::OperationFailed'))
    });
  }

  closePrompt() { this.showResolutionPrompt.set(true); }

  closeClaim(resolution: string) {
    this.claimService.close(this.claimId, resolution).subscribe({
      next: () => { this.toaster.success(this.l('::ClaimClosed')); this.showResolutionPrompt.set(false); this.loadClaim(); },
      error: (err) => this.toaster.error(err?.error?.error?.message || this.l('::OperationFailed'))
    });
  }

  cancelClaim() {
    this.confirmation.warn('::CancelConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.claimService.cancel(this.claimId).subscribe({
        next: () => { this.toaster.success(this.l('::SuccessfullyCancelled')); this.loadClaim(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || this.l('::OperationFailed'))
      });
    });
  }

  private l(key: string): string { return this.localization.instant(key); }
}
