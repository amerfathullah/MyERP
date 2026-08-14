import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { EInvoiceService } from '../proxy/einvoice/einvoice.service';
import { PaginationComponent, type PageEvent } from '../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../shared/services/company-context.service';
import type { EInvoiceSubmissionDto } from '../proxy/einvoice/models';

const STATUS_BADGE: Record<string, string> = {
  Pending:   'bg-warning text-dark',
  Submitted: 'bg-primary',
  Valid:     'bg-success',
  Invalid:   'bg-danger',
  Cancelled: 'bg-secondary',
};

@Component({
  selector: 'app-einvoice-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'EInvoiceSubmissions' | abpLocalization">
      <!-- Summary strip -->
      <div class="row g-3 mb-4">
        <div class="col-6 col-md-3">
          <div class="card border-0 bg-body-tertiary text-center p-3">
            <div class="fs-2 fw-bold text-primary">{{ totalCount() }}</div>
            <div class="small text-muted">Total</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 bg-body-tertiary text-center p-3">
            <div class="fs-2 fw-bold text-success">{{ validCount() }}</div>
            <div class="small text-muted">Valid</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 bg-body-tertiary text-center p-3">
            <div class="fs-2 fw-bold text-danger">{{ invalidCount() }}</div>
            <div class="small text-muted">Invalid</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 bg-body-tertiary text-center p-3">
            <div class="fs-2 fw-bold text-secondary">{{ cancelledCount() }}</div>
            <div class="small text-muted">Cancelled</div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center flex-wrap gap-2">
          <h5 class="mb-0">{{ 'EInvoiceSubmissions' | abpLocalization }}</h5>
          <div class="d-flex gap-2">
            <a class="btn btn-outline-secondary btn-sm" routerLink="/einvoice/settings">
              <i class="fa fa-cog me-1"></i>Settings
            </a>
            <a class="btn btn-primary btn-sm" routerLink="/einvoice/batch-submit">
              <i class="fa fa-paper-plane me-1"></i>Batch Submit
            </a>
          </div>
        </div>

        <!-- Filter bar -->
        <div class="card-body border-bottom py-2">
          <div class="d-flex gap-2 flex-wrap align-items-center">
            <label class="small text-muted mb-0">Filter by status:</label>
            @for (s of statuses; track s) {
              <button class="btn btn-sm"
                [class.btn-secondary]="filterStatus() !== s"
                [class.btn-primary]="filterStatus() === s"
                (click)="setFilter(s)">
                {{ s || 'All' }}
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
              <p class="text-muted">No e-invoice submissions found.</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light"><tr>
                <th>Document</th>
                <th>Type</th>
                <th>Submitted</th>
                <th>UUID</th>
                <th>Status</th>
                <th>QR</th>
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
                      <span class="badge {{ badgeClass(inv.status) }}">{{ inv.status }}</span>
                    </td>
                    <td>
                      @if (inv.qrCodeUrl) {
                        <a [href]="inv.qrCodeUrl" target="_blank" class="btn btn-outline-secondary btn-sm">
                          <i class="fa fa-qrcode"></i>
                        </a>
                      } @else { <span class="text-muted">—</span> }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" title="Refresh status" (click)="refreshStatus(inv)">
                          <i class="fa fa-refresh"></i>
                        </button>
                        @if (inv.status === 'Valid' || inv.status === 'Submitted') {
                          <button class="btn btn-outline-danger" title="Cancel" (click)="cancelInv(inv)">
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
    </abp-page>
  `,
})
export class EInvoiceListComponent implements OnInit {
  private service = inject(EInvoiceService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<EInvoiceSubmissionDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  filterStatus = signal('');
  currentPage = 0;
  pageSize = 50;

  statuses = ['', 'Pending', 'Submitted', 'Valid', 'Invalid', 'Cancelled'];

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
      error: () => this.isLoading.set(false),
    });
  }

  setFilter(status: string): void { this.filterStatus.set(status); }

  badgeClass(status?: string): string { return STATUS_BADGE[status ?? ''] ?? 'bg-secondary'; }

  refreshStatus(inv: EInvoiceSubmissionDto): void {
    this.service.getStatus(inv.id!).subscribe({
      next: updated => {
        this.items.update(list => list.map(i => i.id === updated.id ? updated : i));
        this.toaster.success('Status refreshed');
      },
      error: () => this.toaster.error('Failed to refresh status'),
    });
  }

  cancelInv(inv: EInvoiceSubmissionDto): void {
    this.confirmation.warn('Cancel this e-invoice submission? This cannot be undone within 72 hours.', 'Cancel E-Invoice').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel({ submissionId: inv.id!, reason: 'Cancelled by user' }).subscribe({
        next: updated => {
          this.items.update(list => list.map(i => i.id === updated.id ? updated : i));
          this.toaster.success('Cancelled successfully');
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Cancel failed'),
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
