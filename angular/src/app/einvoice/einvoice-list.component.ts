import { Component, inject, OnInit, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { EInvoiceService } from '../proxy/einvoice/einvoice.service';
import { PaginationComponent, type PageEvent } from '../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../shared/services/company-context.service';
import { LhdnStatusBadgeComponent } from '../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import type { EInvoiceSubmissionDto } from '../proxy/einvoice/models';

@Component({
  selector: 'app-einvoice-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent, LhdnStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::EInvoiceSubmissions' | abpLocalization">
      <!-- Summary strip -->
      <div class="row g-3 mb-4">
        <div class="col-6 col-md-3">
          <div class="card border-0 bg-body-tertiary text-center p-3">
            <div class="fs-2 fw-bold text-primary">{{ totalCount() }}</div>
            <div class="small text-muted">{{ '::Total' | abpLocalization }}</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 bg-body-tertiary text-center p-3">
            <div class="fs-2 fw-bold text-success">{{ validCount() }}</div>
            <div class="small text-muted">{{ '::Valid' | abpLocalization }}</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 bg-body-tertiary text-center p-3">
            <div class="fs-2 fw-bold text-danger">{{ invalidCount() }}</div>
            <div class="small text-muted">{{ '::Invalid' | abpLocalization }}</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 bg-body-tertiary text-center p-3">
            <div class="fs-2 fw-bold text-secondary">{{ cancelledCount() }}</div>
            <div class="small text-muted">{{ '::Cancelled' | abpLocalization }}</div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center flex-wrap gap-2">
          <h5 class="mb-0">{{ '::EInvoiceSubmissions' | abpLocalization }}</h5>
          <div class="d-flex gap-2">
            <a class="btn btn-outline-secondary btn-sm" routerLink="/einvoice/settings">
              <i class="fa fa-cog me-1"></i>{{ '::Settings' | abpLocalization }}
            </a>
            <a class="btn btn-primary btn-sm" routerLink="/einvoice/batch-submit">
              <i class="fa fa-paper-plane me-1"></i>{{ '::BatchSubmit' | abpLocalization }}
            </a>
          </div>
        </div>

        <!-- Filter bar -->
        <div class="card-body border-bottom py-2">
          <div class="d-flex gap-2 flex-wrap align-items-center">
            <label class="small text-muted mb-0">{{ '::FilterByStatus' | abpLocalization }}:</label>
            @for (s of statuses; track s) {
              <button class="btn btn-sm"
                [class.btn-secondary]="filterStatus() !== s"
                [class.btn-primary]="filterStatus() === s"
                (click)="setFilter(s)">
                {{ s || ('::All' | abpLocalization) }}
              </button>
            }
          </div>
        </div>

        <div class="card-body p-0">
          @if (isLoading()) {
            <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x text-muted"></i></div>
          } @else if (filtered().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-file-invoice fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ '::NoEInvoiceSubmissionsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light"><tr>
                <th>{{ '::Document' | abpLocalization }}</th>
                <th>{{ '::Type' | abpLocalization }}</th>
                <th>{{ '::Submitted' | abpLocalization }}</th>
                <th>{{ '::Uuid' | abpLocalization }}</th>
                <th>{{ '::Status' | abpLocalization }}</th>
                <th>{{ '::Qr' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (inv of filtered(); track inv.id) {
                  <tr>
                    <td class="fw-semibold small">{{ inv.sourceDocumentType }}<br>
                      <span class="text-muted">{{ inv.sourceDocumentId }}</span>
                    </td>
                    <td><span class="badge bg-info text-dark">{{ inv.documentTypeCode || '01' }}</span></td>
                    <td class="small">{{ inv.submittedAt ? (inv.submittedAt | date:'dd/MM/yyyy HH:mm') : '—' }}</td>
                    <td class="font-monospace small text-muted" style="max-width:140px;overflow:hidden;text-overflow:ellipsis"
                      [title]="inv.documentUuid ?? ''">
                      {{ (inv.documentUuid ?? '') | slice:0:16 }}{{ inv.documentUuid ? '…' : '' }}
                    </td>
                    <td>
                      <app-lhdn-status-badge [status]="inv.status || 'NotSubmitted'" />
                    </td>
                    <td>
                      @if (inv.qrCodeUrl) {
                        <a [href]="inv.qrCodeUrl" target="_blank" class="btn btn-outline-secondary btn-sm"
                          [title]="'::VerifyOnLhdn' | abpLocalization">
                          <i class="fa fa-qrcode"></i>
                        </a>
                      } @else { <span class="text-muted">—</span> }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" [title]="'::RefreshStatus' | abpLocalization" (click)="refreshStatus(inv)">
                          <i class="fa fa-refresh"></i>
                        </button>
                        @if (inv.status === 'Valid' || inv.status === 'Submitted' || inv.status === 'Accepted' || inv.status === 'Pending') {
                          <button
                            class="btn btn-outline-danger"
                            [disabled]="isCancellationWindowExpired(inv)"
                            [title]="(isCancellationWindowExpired(inv) ? '::CancellationNotAllowedAfter72Hours' : '::CancelEInvoice') | abpLocalization"
                            (click)="openCancelModal(inv)">
                            <i class="fa fa-times"></i>
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize"
        [currentPage]="currentPage" (pageChange)="onPageChange($event)" />

      @if (cancelTarget(); as target) {
        <div class="modal d-block" tabindex="-1" style="background: rgba(0,0,0,0.45)">
          <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content">
              <div class="modal-header py-2">
                <h6 class="modal-title text-danger">
                  <i class="fa fa-triangle-exclamation me-1"></i>{{ '::CancelEInvoice' | abpLocalization }}
                </h6>
                <button type="button" class="btn-close" (click)="closeCancelModal()" [disabled]="isCancelling()"></button>
              </div>
              <div class="modal-body">
                <div class="alert alert-warning small py-2 mb-3">
                  {{ '::CancelEInvoiceWarning' | abpLocalization }}
                </div>
                <div class="small text-muted mb-2">
                  <strong>{{ target.sourceDocumentType }}</strong> · {{ target.documentUuid || target.sourceDocumentId }}
                </div>
                <label class="form-label small fw-semibold">{{ '::CancelReason' | abpLocalization }} *</label>
                <div class="d-flex flex-wrap gap-1 mb-2">
                  @for (preset of cancelPresets; track preset) {
                    <button type="button" class="btn btn-outline-secondary btn-sm py-0 px-2" (click)="cancelReason.set(preset)">
                      {{ preset }}
                    </button>
                  }
                </div>
                <textarea
                  class="form-control form-control-sm"
                  rows="3"
                  [ngModel]="cancelReason()"
                  (ngModelChange)="cancelReason.set($event)"
                  [placeholder]="'::LhdnCancelReasonPlaceholder' | abpLocalization"></textarea>
              </div>
              <div class="modal-footer py-2">
                <button type="button" class="btn btn-sm btn-secondary" (click)="closeCancelModal()" [disabled]="isCancelling()">
                  {{ '::Close' | abpLocalization }}
                </button>
                <button type="button" class="btn btn-sm btn-danger" (click)="confirmCancel()" [disabled]="!cancelReason().trim() || isCancelling()">
                  @if (isCancelling()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  {{ '::ConfirmCancel' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class EInvoiceListComponent implements OnInit {
  private service = inject(EInvoiceService);
  private l = inject(LocalizationService);
  private toaster = inject(ToasterService);

  items = signal<EInvoiceSubmissionDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  filterStatus = signal('');
  cancelTarget = signal<EInvoiceSubmissionDto | null>(null);
  cancelReason = signal('');
  isCancelling = signal(false);
  currentPage = 0;
  pageSize = 50;

  statuses = ['', 'Pending', 'Submitted', 'Valid', 'Invalid', 'Cancelled'];
  cancelPresets = ['Wrong buyer details', 'Incorrect invoice amount', 'Duplicate submission', 'Order cancelled'];

  filtered = computed(() => {
    const s = this.filterStatus();
    if (!s) return this.items();
    return this.items().filter(i => i.status === s);
  });

  validCount = computed(() => this.items().filter(i => i.status === 'Valid').length);
  invalidCount = computed(() => this.items().filter(i => i.status === 'Invalid').length);
  cancelledCount = computed(() => this.items().filter(i => i.status === 'Cancelled').length);

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: (err: any) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.data?.detail || err?.error?.error?.message || this.l.instant('::FailedToLoadEInvoices'));
      },
    });
  }

  setFilter(status: string): void { this.filterStatus.set(status); }

  refreshStatus(inv: EInvoiceSubmissionDto): void {
    this.service.getStatus(inv.id!).subscribe({
      next: updated => {
        this.items.update(list => list.map(i => i.id === updated.id ? updated : i));
        this.toaster.success(this.l.instant('::StatusRefreshed'));
      },
      error: () => this.toaster.error(this.l.instant('::FailedToRefreshStatus')),
    });
  }

  isCancellationWindowExpired(inv: EInvoiceSubmissionDto): boolean {
    if (!inv?.submittedAt) return false;
    const submittedMs = Date.parse(inv.submittedAt);
    if (Number.isNaN(submittedMs)) return false;
    return Date.now() - submittedMs > 72 * 60 * 60 * 1000;
  }

  openCancelModal(inv: EInvoiceSubmissionDto): void {
    if (this.isCancellationWindowExpired(inv)) return;
    this.cancelTarget.set(inv);
    this.cancelReason.set('');
  }

  closeCancelModal(): void {
    if (this.isCancelling()) return;
    this.cancelTarget.set(null);
    this.cancelReason.set('');
  }

  confirmCancel(): void {
    const target = this.cancelTarget();
    const reason = this.cancelReason().trim();
    if (!target?.id || !reason || this.isCancelling()) return;

    this.isCancelling.set(true);
    this.service.cancel({ submissionId: target.id, reason }).subscribe({
      next: updated => {
        this.isCancelling.set(false);
        this.cancelTarget.set(null);
        this.cancelReason.set('');
        this.items.update(list => list.map(i => i.id === updated.id ? updated : i));
        this.toaster.success(this.l.instant('::SuccessfullyCancelled'));
      },
      error: (err: any) => {
        this.isCancelling.set(false);
        this.toaster.error(err?.error?.error?.message ?? this.l.instant('::CancelFailed'));
      },
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
