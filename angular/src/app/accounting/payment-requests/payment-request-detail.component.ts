import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { PaymentRequestService } from '../../proxy/accounting/payment-request.service';
import type { PaymentRequestDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-payment-request-detail',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    @if (entity(); as pr) {
      <div class="container-fluid my-3">
        <!-- KPI Cards -->
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm">
              <div class="card-body text-center">
                <div class="text-muted small mb-1">{{ '::Status' | abpLocalization }}</div>
                <app-status-badge [status]="statusLabel()" />
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm">
              <div class="card-body text-center">
                <div class="text-muted small mb-1">{{ '::Amount' | abpLocalization }}</div>
                <div class="fs-5 fw-bold text-primary">{{ pr.grandTotal | number:'1.2-2' }}</div>
                <div class="text-muted small">{{ pr.currency || 'MYR' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm">
              <div class="card-body text-center">
                <div class="text-muted small mb-1">{{ '::Outstanding' | abpLocalization }}</div>
                <div class="fs-5 fw-bold" [class.text-danger]="(pr.outstandingAmount ?? 0) > 0" [class.text-success]="(pr.outstandingAmount ?? 0) <= 0">
                  {{ pr.outstandingAmount | number:'1.2-2' }}
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm">
              <div class="card-body text-center">
                <div class="text-muted small mb-1">{{ '::PartyType' | abpLocalization }}</div>
                <div class="fw-semibold">{{ pr.partyType || '—' }}</div>
                <div class="text-muted small text-truncate">{{ pr.partyName || '—' }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Details Card -->
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0"><i class="fas fa-credit-card me-2"></i>{{ '::PaymentRequestDetails' | abpLocalization }}</h6>
            <div class="btn-group btn-group-sm">
              @if (statusNum() === 0) {
                <button class="btn btn-outline-success" (click)="submit()">
                  <i class="fas fa-paper-plane me-1"></i>{{ '::Submit' | abpLocalization }}
                </button>
              }
              @if (statusNum() === 1) {
                <button class="btn btn-outline-primary" (click)="pay()" [disabled]="paying()">
                  @if (paying()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  <i class="fas fa-money-check-alt me-1"></i>{{ '::CreatePaymentEntry' | abpLocalization }}
                </button>
                <button class="btn btn-outline-danger" (click)="cancel()">
                  <i class="fas fa-ban me-1"></i>{{ '::Cancel' | abpLocalization }}
                </button>
              }
            </div>
          </div>
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-4">
                <div class="text-muted small">{{ '::ReferenceNumber' | abpLocalization }}</div>
                <div class="fw-semibold">{{ pr.referenceDoctype || '—' }}</div>
              </div>
              <div class="col-md-4">
                <div class="text-muted small">{{ '::PaymentType' | abpLocalization }}</div>
                <div class="fw-semibold">{{ pr.paymentRequestType || '—' }}</div>
              </div>
              <div class="col-md-4">
                <div class="text-muted small">{{ '::Currency' | abpLocalization }}</div>
                <div class="fw-semibold">{{ pr.currency || 'MYR' }}</div>
              </div>
            </div>
            @if (pr.paymentEntryId) {
              <div class="mt-3">
                <div class="text-muted small">{{ '::PaymentEntry' | abpLocalization }}</div>
                <div class="fw-semibold text-success">
                  <i class="fas fa-check-circle me-1"></i>{{ '::Paid' | abpLocalization }}
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Activity Log -->
        <app-activity-log documentType="PaymentRequest" [documentId]="pr.id!" />
      </div>
    } @else {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    }
  `,
  styles: [``]
})
export class PaymentRequestDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(PaymentRequestService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  entity = signal<PaymentRequestDto | null>(null);
  statusNum = signal(0);
  paying = signal(false);

  statusLabel(): string {
    const labels: Record<number, string> = { 0: 'Draft', 1: 'Initiated', 2: 'Paid', 3: 'Cancelled' };
    return labels[this.statusNum()] ?? 'Draft';
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({
      next: (r) => {
        this.entity.set(r);
        this.statusNum.set(r.status ?? 0);
      },
      error: () => {}
    });
  }

  submit(): void {
    this.service.submit(this.entity()!.id!).subscribe({
      next: () => {
        this.toaster.success(this.l.instant('::SuccessfullySubmitted'));
        this.reload();
      },
      error: () => {}
    });
  }

  pay(): void {
    this.paying.set(true);
    this.service.pay(this.entity()!.id!).subscribe({
      next: () => {
        this.toaster.success(this.l.instant('::SuccessfullyPaid'));
        this.paying.set(false);
        this.reload();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? 'OperationFailed');
        this.paying.set(false);
      },
    });
  }

  cancel(): void {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(this.entity()!.id!).subscribe({
        next: () => {
          this.toaster.success(this.l.instant('::SuccessfullyCancelled'));
          this.reload();
        },
        error: () => {}
      });
    });
  }

  private reload(): void {
    this.service.get(this.entity()!.id!).subscribe({
      next: (r) => {
        this.entity.set(r);
        this.statusNum.set(r.status ?? 0);
      },
      error: () => {}
    });
  }
}
