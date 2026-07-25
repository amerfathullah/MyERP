import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MonthEndCloseService } from '../../proxy/accounting/month-end-close.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

interface MonthEndCheck {
  name: string;
  passed: boolean;
  details: string | null;
}

interface ReadinessResult {
  isReady?: boolean;
  passedCount?: number;
  totalChecks?: number;
  checks?: MonthEndCheck[];
}

interface CloseStatus {
  isTrialBalanceBalanced?: boolean;
  hasPeriodClosingVoucher?: boolean;
  isPeriodClosed?: boolean;
  isFullyClosed?: boolean;
}

@Component({
  standalone: true,
  selector: 'app-month-end-close',
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid">
      <div class="row mb-4">
        <div class="col">
          <h4>{{ 'MonthEndClose' | abpLocalization }}</h4>
          <p class="text-muted">{{ 'ValidateReadiness' | abpLocalization }}</p>
        </div>
      </div>

      <!-- Period Selection -->
      <div class="row mb-4">
        <div class="col-md-4">
          <div class="card">
            <div class="card-body">
              <label class="form-label fw-bold">{{ 'PeriodEndDate' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="periodEndDate" />
              <div class="mt-3 d-flex gap-2">
                <button class="btn btn-primary" (click)="validateReadiness()" [disabled]="loading()">
                  <i class="fa fa-clipboard-check me-1"></i>{{ 'ValidateReadiness' | abpLocalization }}
                </button>
                <button class="btn btn-outline-secondary" (click)="checkStatus()" [disabled]="loading()">
                  <i class="fa fa-info-circle me-1"></i>{{ 'CloseStatus' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Close Status Card -->
        @if (closeStatus()) {
        <div class="col-md-4">
          <div class="card" [class.border-success]="closeStatus()!.isFullyClosed" [class.border-warning]="!closeStatus()!.isFullyClosed">
            <div class="card-header">
              <i class="fa fa-lock me-2"></i>{{ 'CloseStatus' | abpLocalization }}
            </div>
            <div class="card-body">
              <ul class="list-unstyled mb-0">
                <li class="mb-2">
                  <i class="fa" [class.fa-check-circle]="closeStatus()!.isTrialBalanceBalanced" [class.fa-times-circle]="!closeStatus()!.isTrialBalanceBalanced" [class.text-success]="closeStatus()!.isTrialBalanceBalanced" [class.text-danger]="!closeStatus()!.isTrialBalanceBalanced"></i>
                  {{ 'TrialBalanceBalanced' | abpLocalization }}
                </li>
                <li class="mb-2">
                  <i class="fa" [class.fa-check-circle]="closeStatus()!.hasPeriodClosingVoucher" [class.fa-times-circle]="!closeStatus()!.hasPeriodClosingVoucher" [class.text-success]="closeStatus()!.hasPeriodClosingVoucher" [class.text-danger]="!closeStatus()!.hasPeriodClosingVoucher"></i>
                  {{ 'PeriodClosingVoucher' | abpLocalization }}
                </li>
                <li>
                  <i class="fa" [class.fa-check-circle]="closeStatus()!.isPeriodClosed" [class.fa-times-circle]="!closeStatus()!.isPeriodClosed" [class.text-success]="closeStatus()!.isPeriodClosed" [class.text-danger]="!closeStatus()!.isPeriodClosed"></i>
                  {{ 'PeriodClosed' | abpLocalization }}
                </li>
              </ul>
              @if (closeStatus()!.isFullyClosed) {
              <div class="alert alert-success mt-3 mb-0 py-2">
                <i class="fa fa-check-double me-1"></i>{{ 'FullyClosed' | abpLocalization }}
              </div>
              }
            </div>
          </div>
        </div>
        }

        <!-- Freeze Card -->
        <div class="col-md-4">
          <div class="card">
            <div class="card-header">
              <i class="fa fa-snowflake me-2"></i>{{ 'FreezeAccounts' | abpLocalization }}
            </div>
            <div class="card-body">
              <label class="form-label">{{ 'FreezeUpTo' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="freezeDate" />
              <button class="btn btn-warning btn-sm mt-2" (click)="freezeAccounts()" [disabled]="!freezeDate">
                <i class="fa fa-lock me-1"></i>{{ 'FreezeAccounts' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Readiness Checklist -->
      @if (readiness()) {
      <div class="row">
        <div class="col-12">
          <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
              <span><i class="fa fa-list-check me-2"></i>{{ 'ValidateReadiness' | abpLocalization }}</span>
              <span class="badge" [class.bg-success]="readiness()!.isReady" [class.bg-danger]="!readiness()!.isReady">
                {{ readiness()!.passedCount }}/{{ readiness()!.totalChecks }} {{ 'ChecksPassed' | abpLocalization }}
              </span>
            </div>
            <div class="card-body p-0">
              <table class="table table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th style="width: 50px"></th>
                    <th>Check</th>
                    <th>Details</th>
                  </tr>
                </thead>
                <tbody>
                  @for (check of readiness()!.checks; track check.name) {
                  <tr>
                    <td class="text-center">
                      <i class="fa" [class.fa-circle-check]="check.passed" [class.fa-circle-xmark]="!check.passed" [class.text-success]="check.passed" [class.text-danger]="!check.passed"></i>
                    </td>
                    <td>{{ check.name }}</td>
                    <td><span class="text-muted">{{ check.details || '—' }}</span></td>
                  </tr>
                  }
                </tbody>
              </table>
            </div>
            @if (readiness()!.isReady) {
            <div class="card-footer">
              <div class="alert alert-success mb-0 py-2">
                <i class="fa fa-check-double me-1"></i>{{ 'ReadyForClose' | abpLocalization }} — proceed with Period Closing Voucher creation.
              </div>
            </div>
            }
          </div>
        </div>
      </div>
      }
    </div>
  `
})
export class MonthEndCloseComponent {
  private monthEndCloseService = inject(MonthEndCloseService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  periodEndDate = new Date().toISOString().split('T')[0];
  freezeDate = '';
  loading = signal(false);
  readiness = signal<ReadinessResult | null>(null);
  closeStatus = signal<CloseStatus | null>(null);

  validateReadiness() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) { this.toaster.warn('Select a company first'); return; }

    this.loading.set(true);
    this.monthEndCloseService.validateReadiness({
      companyId,
      periodEndDate: this.periodEndDate
    }).subscribe({
      next: (res) => { this.readiness.set(res as ReadinessResult); this.loading.set(false); },
      error: () => { this.loading.set(false); }
    });
  }

  checkStatus() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) { this.toaster.warn('Select a company first'); return; }

    this.loading.set(true);
    this.monthEndCloseService.getCloseStatus({
      companyId,
      periodEndDate: this.periodEndDate
    }).subscribe({
      next: (res) => { this.closeStatus.set(res as CloseStatus); this.loading.set(false); },
      error: () => { this.loading.set(false); }
    });
  }

  freezeAccounts() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) { this.toaster.warn('Select a company first'); return; }

    this.monthEndCloseService.freeze({
      companyId,
      freezeUpTo: this.freezeDate
    }).subscribe({
      next: () => { this.toaster.success('Accounting period frozen'); },
      error: () => {}
    });
  }
}
