import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PaymentLedgerRepostService } from '../../proxy/accounting/payment-ledger-repost.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { PaymentLedgerRepostResultDto } from '../../proxy/accounting/models';

const ALLOWED_TYPES = ['SalesInvoice', 'PurchaseInvoice', 'PaymentEntry', 'JournalEntry'];

@Component({
  selector: 'app-payment-ledger-repost',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid mt-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4><i class="fas fa-rotate me-2"></i>{{ 'PaymentLedgerRepost' | abpLocalization }}</h4>
      </div>

      <!-- Single Voucher Repost -->
      <div class="card mb-3">
        <div class="card-header">
          <h6 class="mb-0">{{ 'RepostSingleVoucher' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          <div class="row g-2 align-items-end">
            <div class="col-md-4">
              <label class="form-label">{{ 'VoucherType' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="voucherType">
                @for (t of allowedTypes; track t) {
                  <option [value]="t">{{ t }}</option>
                }
              </select>
            </div>
            <div class="col-md-5">
              <label class="form-label">{{ 'VoucherId' | abpLocalization }}</label>
              <input type="text" class="form-control form-control-sm" [(ngModel)]="voucherId"
                     [placeholder]="'::Placeholder:EnterDocumentId' | abpLocalization">
            </div>
            <div class="col-md-3">
              <button class="btn btn-warning btn-sm w-100" (click)="repostSingle()" [disabled]="isProcessing()">
                <i class="fas fa-rotate me-1"></i> {{ 'Repost' | abpLocalization }}
              </button>
            </div>
          </div>
          <p class="text-muted small mt-2 mb-0">
            <i class="fas fa-info-circle me-1"></i>
            Deletes existing Payment Ledger Entries for this voucher and rebuilds them from its GL entries.
          </p>
        </div>
      </div>

      <!-- Company-wide Repost -->
      <div class="card mb-3">
        <div class="card-header">
          <h6 class="mb-0">{{ 'RepostForCompany' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          <div class="row g-2 align-items-end">
            <div class="col-md-6">
              <label class="form-label">{{ 'FromDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
            </div>
            <div class="col-md-3">
              <button class="btn btn-warning btn-sm w-100" (click)="repostCompany()" [disabled]="isProcessing() || !fromDate">
                <i class="fas fa-rotate me-1"></i> {{ 'RepostAll' | abpLocalization }}
              </button>
            </div>
          </div>
          <p class="text-muted small mt-2 mb-0">
            <i class="fas fa-info-circle me-1"></i>
            Reposts every posted Sales Invoice, Purchase Invoice, Payment Entry, and Journal Entry on or after this date.
          </p>
        </div>
      </div>

      @if (lastResult()) {
        <div class="card mb-3" [class.border-success]="!lastResult()!.hasErrors" [class.border-danger]="lastResult()!.hasErrors">
          <div class="card-body">
            <div class="row text-center">
              <div class="col-md-4">
                <div class="text-muted small">{{ 'TotalVouchers' | abpLocalization }}</div>
                <div class="fw-bold fs-5">{{ lastResult()!.totalVouchers }}</div>
              </div>
              <div class="col-md-4">
                <div class="text-muted small">{{ 'Succeeded' | abpLocalization }}</div>
                <div class="fw-bold fs-5 text-success">{{ lastResult()!.successCount }}</div>
              </div>
              <div class="col-md-4">
                <div class="text-muted small">{{ 'Failed' | abpLocalization }}</div>
                <div class="fw-bold fs-5 text-danger">{{ lastResult()!.failedCount }}</div>
              </div>
            </div>
            @if (lastResult()!.errors && lastResult()!.errors!.length > 0) {
              <div class="mt-3">
                <h6 class="text-danger">Errors:</h6>
                <ul class="list-unstyled mb-0">
                  @for (err of lastResult()!.errors; track err) {
                    <li class="text-danger small"><i class="fas fa-exclamation-triangle me-1"></i>{{ err }}</li>
                  }
                </ul>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class PaymentLedgerRepostComponent {
  private service = inject(PaymentLedgerRepostService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  allowedTypes = ALLOWED_TYPES;
  lastResult = signal<PaymentLedgerRepostResultDto | null>(null);
  isProcessing = signal(false);
  voucherType = 'SalesInvoice';
  voucherId = '';
  fromDate = '';

  repostSingle(): void {
    const cid = this.companyContext.currentCompanyId();
    if (!cid || !this.voucherId) return;

    this.isProcessing.set(true);
    this.service.repost({
      companyId: cid,
      voucherType: this.voucherType,
      voucherId: this.voucherId,
    }).subscribe({
      next: result => {
        this.lastResult.set(result);
        this.isProcessing.set(false);
        if (result.successCount) this.toaster.success('::SuccessfullyReposted');
      },
      error: () => this.isProcessing.set(false),
    });
  }

  repostCompany(): void {
    const cid = this.companyContext.currentCompanyId();
    if (!cid || !this.fromDate) return;

    this.isProcessing.set(true);
    this.service.repostForCompany({
      companyId: cid,
      fromDate: this.fromDate,
    }).subscribe({
      next: result => {
        this.lastResult.set(result);
        this.isProcessing.set(false);
        this.toaster.success('::SuccessfullyReposted');
      },
      error: () => this.isProcessing.set(false),
    });
  }
}
