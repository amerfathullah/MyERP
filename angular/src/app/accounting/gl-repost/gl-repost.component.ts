import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe , LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface RepostResultDto {
  successCount: number;
  skippedCount: number;
  failedCount: number;
  totalProcessed: number;
  hasErrors: boolean;
  errors: string[];
}

@Component({
  selector: 'app-gl-repost',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid mt-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4><i class="fas fa-rotate me-2"></i>{{ 'GLRepost' | abpLocalization }}</h4>
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
                @for (t of allowedTypes(); track t) {
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
            Reposts GL entries by deleting existing journal and regenerating from current accounting rules.
          </p>
        </div>
      </div>

      <!-- Result Display -->
      @if (lastResult()) {
        <div class="card mb-3" [class.border-success]="!lastResult()!.hasErrors" [class.border-danger]="lastResult()!.hasErrors">
          <div class="card-body">
            <div class="row text-center">
              <div class="col-md-3">
                <div class="text-muted small">{{ 'Processed' | abpLocalization }}</div>
                <div class="fw-bold fs-5">{{ lastResult()!.totalProcessed }}</div>
              </div>
              <div class="col-md-3">
                <div class="text-muted small">{{ 'Succeeded' | abpLocalization }}</div>
                <div class="fw-bold fs-5 text-success">{{ lastResult()!.successCount }}</div>
              </div>
              <div class="col-md-3">
                <div class="text-muted small">{{ 'Skipped' | abpLocalization }}</div>
                <div class="fw-bold fs-5 text-warning">{{ lastResult()!.skippedCount }}</div>
              </div>
              <div class="col-md-3">
                <div class="text-muted small">{{ 'Failed' | abpLocalization }}</div>
                <div class="fw-bold fs-5 text-danger">{{ lastResult()!.failedCount }}</div>
              </div>
            </div>
            @if (lastResult()!.errors.length > 0) {
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

      <!-- Allowed Types Info -->
      <div class="card">
        <div class="card-header">
          <h6 class="mb-0">{{ 'AllowedVoucherTypes' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          <div class="d-flex flex-wrap gap-2">
            @for (t of allowedTypes(); track t) {
              <span class="badge bg-info">{{ t }}</span>
            }
          </div>
          <p class="text-muted small mt-2 mb-0">
            Per ERPNext: only these document types support GL repost. Other types must be cancelled and re-submitted.
          </p>
        </div>
      </div>
    </div>
  `
})
export class GlRepostComponent {
  private http = inject(HttpClient);
  private localization = inject(LocalizationService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  allowedTypes = signal<string[]>([]);
  lastResult = signal<RepostResultDto | null>(null);
  isProcessing = signal(false);
  voucherType = 'SalesInvoice';
  voucherId = '';

  constructor() {
    this.loadAllowedTypes();
  }

  loadAllowedTypes(): void {
    this.http.get<string[]>('/api/app/gl-repost/allowed-voucher-types')
      .subscribe({
        next: types => this.allowedTypes.set(types),
        error: () => this.allowedTypes.set([
          'SalesInvoice', 'PurchaseInvoice', 'PaymentEntry',
          'JournalEntry', 'PurchaseReceipt', 'DeliveryNote', 'StockEntry'
        ])
      });
  }

  repostSingle(): void {
    const cid = this.companyContext.currentCompanyId();
    if (!cid || !this.voucherId) return;

    this.isProcessing.set(true);
    this.http.post<RepostResultDto>('/api/app/gl-repost/repost', {
      companyId: cid,
      voucherType: this.voucherType,
      voucherId: this.voucherId
    }).subscribe({
      next: result => {
        this.lastResult.set(result);
        this.isProcessing.set(false);
        if (result.successCount > 0) {
          this.toaster.success('::SuccessfullyReposted');
        } else if (result.skippedCount > 0) {
          this.toaster.warn(this.localization.instant('::RepostSkipped'), this.localization.instant('::Skipped'));
        }
      },
      error: () => this.isProcessing.set(false)
    });
  }
}
