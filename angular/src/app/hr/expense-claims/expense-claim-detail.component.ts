import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ExpenseClaimService } from '../../proxy/human-resources/expense-claim.service';
import type { ExpenseClaimDto } from '../../proxy/human-resources/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-expense-claim-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, ActivityLogComponent],
  template: `
    <abp-page [title]="'ExpenseClaims' | abpLocalization">
      <app-breadcrumb />
      @if (claim) {
        <!-- KPI Cards -->
        <div class="row mb-4">
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted d-block">{{ '::Status' | abpLocalization }}</small>
                <span class="badge mt-1" [ngClass]="statusClass(claim.status)">{{ statusLabel(claim.status) }}</span>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted d-block">{{ '::TotalClaimed' | abpLocalization }}</small>
                <span class="fw-bold fs-5 mt-1">{{ claim.totalClaimedAmount | number:'1.2-2' }}</span>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-success">
              <div class="card-body text-center">
                <small class="text-muted d-block">{{ '::Reimbursed' | abpLocalization }}</small>
                <span class="fw-bold fs-5 mt-1 text-success">{{ claim.totalAmountReimbursed ?? 0 | number:'1.2-2' }}</span>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card" [class.border-warning]="reimbursablePending() > 0">
              <div class="card-body text-center">
                <small class="text-muted d-block">{{ '::PendingReimbursement' | abpLocalization }}</small>
                <span class="fw-bold fs-5 mt-1" [class.text-warning]="reimbursablePending() > 0">{{ reimbursablePending() | number:'1.2-2' }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Reimbursement Progress Bar (visible after approval) -->
        @if (claim.status >= 2 && claim.totalClaimedAmount > 0) {
          <div class="card mb-3">
            <div class="card-body py-2">
              <div class="d-flex justify-content-between mb-1">
                <small class="fw-bold">{{ '::ReimbursementProgress' | abpLocalization }}</small>
                <small [class.text-success]="reimbursementPct() >= 100" [class.fw-bold]="reimbursementPct() >= 100">
                  {{ reimbursementPct() | number:'1.0-0' }}%
                </small>
              </div>
              <div class="progress" style="height: 8px;">
                <div class="progress-bar bg-success" [style.width.%]="reimbursementPct()"></div>
              </div>
            </div>
          </div>
        }

        <!-- Header Info -->
        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-4 mb-2">
                <small class="text-muted d-block">{{ '::Employee' | abpLocalization }}</small>
                <span class="fw-semibold">{{ claim.employeeName }}</span>
              </div>
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ '::Date' | abpLocalization }}</small>
                <span class="fw-semibold">{{ claim.postingDate | date:'dd/MM/yyyy' }}</span>
              </div>
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ '::ExpenseType' | abpLocalization }}</small>
                <span class="fw-semibold">{{ claim.expenseType }}</span>
              </div>
              @if (claim.advanceAmount) {
                <div class="col-md-2 mb-2">
                  <small class="text-muted d-block">{{ '::AdvanceAmount' | abpLocalization }}</small>
                  <span class="fw-semibold text-info">{{ claim.advanceAmount | number:'1.2-2' }}</span>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Expense Lines -->
        <div class="card mb-3">
          <div class="card-header">
            <h6 class="mb-0"><i class="fa fa-receipt me-2"></i>{{ '::Expenses' | abpLocalization }}</h6>
          </div>
          <div class="card-body p-0">
            <table class="table table-sm mb-0">
              <thead>
                <tr class="table-light">
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (e of claim.expenses ?? []; track e.id) {
                  <tr>
                    <td>{{ e.expenseDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ e.description }}</td>
                    <td class="text-end font-monospace">{{ e.amount | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr class="table-light">
                  <th colspan="2">{{ '::Total' | abpLocalization }}</th>
                  <th class="text-end font-monospace">{{ claim.totalClaimedAmount | number:'1.2-2' }}</th>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        <!-- Workflow Actions -->
        <div class="d-flex gap-2 mb-4">
          @if (claim.status === 0) {
            <button class="btn btn-primary btn-sm" (click)="submit()" [disabled]="actionLoading()">
              <i class="fa fa-paper-plane me-1"></i>{{ '::Submit' | abpLocalization }}
            </button>
          }
          @if (claim.status === 1) {
            <button class="btn btn-success btn-sm" (click)="approve()" [disabled]="actionLoading()">
              <i class="fa fa-check me-1"></i>{{ '::Approve' | abpLocalization }}
            </button>
            <button class="btn btn-danger btn-sm" (click)="reject()" [disabled]="actionLoading()">
              <i class="fa fa-times me-1"></i>{{ '::Reject' | abpLocalization }}
            </button>
          }
          @if (claim.status === 2 && reimbursablePending() > 0.01) {
            <button class="btn btn-success btn-sm" (click)="reimburse()" [disabled]="actionLoading()">
              @if (actionLoading()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              <i class="fa fa-money-bill-transfer me-1"></i>{{ '::Reimburse' | abpLocalization }}
            </button>
          }
          @if (claim.status !== 4 && claim.status !== 5) {
            <button class="btn btn-outline-danger btn-sm" (click)="cancel()" [disabled]="actionLoading()">
              <i class="fa fa-ban me-1"></i>{{ '::Cancel' | abpLocalization }}
            </button>
          }
        </div>

        <app-activity-log documentType="ExpenseClaim" [documentId]="claim.id!" />
      }
    </abp-page>
  `,
})
export class ExpenseClaimDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(ExpenseClaimService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);
  private confirmation = inject(ConfirmationService);

  claim: ExpenseClaimDto | null = null;
  actionLoading = signal(false);

  private get id() { return this.route.snapshot.paramMap.get('id')!; }

  ngOnInit() { this.load(); }

  load() {
    this.service.get(this.id).subscribe({ next: (r) => this.claim = r, error: () => {} });
  }

  reimbursablePending(): number {
    if (!this.claim) return 0;
    return Math.max(0, (this.claim.totalClaimedAmount ?? 0) - (this.claim.advanceAmount ?? 0) - (this.claim.totalAmountReimbursed ?? 0));
  }

  reimbursementPct(): number {
    if (!this.claim || !this.claim.totalClaimedAmount) return 0;
    return Math.min(100, ((this.claim.totalAmountReimbursed ?? 0) / this.claim.totalClaimedAmount) * 100);
  }

  submit() {
    this.actionLoading.set(true);
    this.service.submit(this.id).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.load(); this.actionLoading.set(false); },
      error: (err: any) => { this.toaster.error(err?.error?.error?.message || '::OperationFailed'); this.actionLoading.set(false); }
    });
  }

  approve() {
    this.actionLoading.set(true);
    this.service.approve(this.id).subscribe({
      next: () => { this.toaster.success('::SuccessfullyApproved'); this.load(); this.actionLoading.set(false); },
      error: (err: any) => { this.toaster.error(err?.error?.error?.message || '::OperationFailed'); this.actionLoading.set(false); }
    });
  }

  reject() {
    this.actionLoading.set(true);
    this.service.reject(this.id).subscribe({
      next: () => { this.toaster.success('::SuccessfullyRejected'); this.load(); this.actionLoading.set(false); },
      error: (err: any) => { this.toaster.error(err?.error?.error?.message || '::OperationFailed'); this.actionLoading.set(false); }
    });
  }

  reimburse() {
    this.actionLoading.set(true);
    // Use company default bank account - backend resolves the appropriate account
    this.service.reimburse(this.id, '').subscribe({
      next: () => {
        this.toaster.success('::ReimbursementCreated');
        this.load();
        this.actionLoading.set(false);
      },
      error: (err: any) => { this.toaster.error(err?.error?.error?.message || '::OperationFailed'); this.actionLoading.set(false); }
    });
  }

  cancel() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.actionLoading.set(true);
      this.service.cancel(this.id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.load(); this.actionLoading.set(false); },
        error: (err: any) => { this.toaster.error(err?.error?.error?.message || '::OperationFailed'); this.actionLoading.set(false); }
      });
    });
  }

  statusLabel(s: number | undefined) {
    const keys = ['Draft', 'Submitted', 'Approved', '', 'Cancelled', 'Rejected'];
    const key = keys[s ?? 0] || 'Draft';
    return key ? this.localization.instant('::' + key) : '';
  }

  statusClass(s: number | undefined) {
    return ['bg-secondary', 'bg-primary', 'bg-success', '', 'bg-danger', 'bg-warning'][s ?? 0] ?? 'bg-secondary';
  }
}
