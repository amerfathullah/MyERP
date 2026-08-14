import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { EInvoiceService } from '../proxy/einvoice/einvoice.service';
import { SalesInvoiceService } from '../proxy/sales/sales-invoice.service';
import { CompanyContextService } from '../shared/services/company-context.service';
import type { BatchSubmitResultDto } from '../proxy/einvoice/models';

@Component({
  selector: 'app-einvoice-batch-submit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'BatchSubmitEInvoice' | abpLocalization">
      <div class="card mb-4">
        <div class="card-header">
          <h6 class="mb-0"><i class="fa fa-paper-plane me-2"></i>Batch Submit to LHDN MyInvois</h6>
        </div>
        <div class="card-body">
          <div class="row g-3 mb-4">
            <div class="col-md-4">
              <label class="form-label">Document Type</label>
              <select class="form-select" [(ngModel)]="docType">
                <option value="SalesInvoice">Sales Invoice</option>
                <option value="CreditNote">Credit Note</option>
                <option value="DebitNote">Debit Note</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">From Date</label>
              <input type="date" class="form-control" [(ngModel)]="fromDate" />
            </div>
            <div class="col-md-3">
              <label class="form-label">To Date</label>
              <input type="date" class="form-control" [(ngModel)]="toDate" />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button class="btn btn-outline-primary w-100" (click)="loadInvoices()" [disabled]="loadingInvoices()">
                @if (loadingInvoices()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                Load
              </button>
            </div>
          </div>

          @if (invoices().length > 0) {
            <div class="d-flex justify-content-between align-items-center mb-2">
              <span class="small text-muted">{{ invoices().length }} documents loaded — {{ selected().length }} selected</span>
              <div class="d-flex gap-2">
                <button class="btn btn-outline-secondary btn-sm" (click)="selectAll()">Select All</button>
                <button class="btn btn-outline-secondary btn-sm" (click)="clearAll()">Clear</button>
              </div>
            </div>

            <div class="table-responsive mb-3" style="max-height:350px;overflow-y:auto">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light sticky-top"><tr>
                  <th style="width:40px">
                    <input type="checkbox" [checked]="allSelected()" (change)="toggleAll($event)" />
                  </th>
                  <th>Document #</th>
                  <th>Customer</th>
                  <th class="text-end">Amount</th>
                  <th>Date</th>
                  <th>e-Invoice Status</th>
                </tr></thead>
                <tbody>
                  @for (inv of invoices(); track inv.id) {
                    <tr [class.table-active]="isSelected(inv.id)">
                      <td>
                        <input type="checkbox" [checked]="isSelected(inv.id)" (change)="toggleOne(inv.id, $event)"
                          [disabled]="inv.eInvoiceStatus === 'Valid'" />
                      </td>
                      <td class="fw-semibold small">{{ inv.invoiceNumber || inv.id }}</td>
                      <td class="small">{{ inv.customerName || '—' }}</td>
                      <td class="text-end small">{{ (inv.grandTotal ?? 0) | number:'1.2-2' }}</td>
                      <td class="small">{{ inv.postingDate ? (inv.postingDate | date:'dd/MM/yy') : '—' }}</td>
                      <td>
                        @if (inv.eInvoiceStatus) {
                          <span class="badge" [class]="eiBadge(inv.eInvoiceStatus)">{{ inv.eInvoiceStatus }}</span>
                        } @else {
                          <span class="badge bg-light text-dark border">Not Submitted</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <button class="btn btn-primary" (click)="submitBatch()" [disabled]="selected().length === 0 || submitting()">
              @if (submitting()) { <i class="fa fa-spinner fa-spin me-1"></i> }
              Submit {{ selected().length }} Document(s) to LHDN
            </button>
          }
        </div>
      </div>

      <!-- Results -->
      @if (results()) {
        <div class="card">
          <div class="card-header">
            <h6 class="mb-0">
              Submission Results:
              <span class="badge bg-success ms-2">{{ results()!.succeededCount }} OK</span>
              @if ((results()!.failedCount ?? 0) > 0) {
                <span class="badge bg-danger ms-1">{{ results()!.failedCount }} Failed</span>
              }
              @if ((results()!.skippedCount ?? 0) > 0) {
                <span class="badge bg-warning text-dark ms-1">{{ results()!.skippedCount }} Skipped</span>
              }
            </h6>
          </div>
          <div class="card-body p-0">
            <table class="table table-sm mb-0">
              <thead><tr>
                <th>Document</th>
                <th>Result</th>
                <th>UUID</th>
                <th>Details</th>
              </tr></thead>
              <tbody>
                @for (r of results()!.results ?? []; track r.documentId) {
                  <tr [class.table-success]="r.success" [class.table-danger]="!r.success">
                    <td>{{ r.documentNumber || r.documentId }}</td>
                    <td>
                      <span class="badge" [class.bg-success]="r.success" [class.bg-danger]="!r.success">
                        {{ r.success ? 'Success' : 'Failed' }}
                      </span>
                    </td>
                    <td class="font-monospace small">{{ r.lhdnUuid || '—' }}</td>
                    <td class="small text-muted">{{ r.errorMessage || r.status || '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <div class="mt-3">
        <a class="btn btn-secondary" routerLink="/einvoice">← Back to Submissions</a>
      </div>
    </abp-page>
  `,
})
export class EInvoiceBatchSubmitComponent {
  private eiService = inject(EInvoiceService);
  private invoiceService = inject(SalesInvoiceService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  docType = 'SalesInvoice';
  fromDate = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];
  toDate = new Date().toISOString().split('T')[0];

  invoices = signal<any[]>([]);
  selectedIds = signal<Set<string>>(new Set());
  loadingInvoices = signal(false);
  submitting = signal(false);
  results = signal<BatchSubmitResultDto | null>(null);

  selected = computed(() => [...this.selectedIds()]);
  allSelected = computed(() => this.invoices().length > 0 && this.selectedIds().size === this.invoices().length);

  loadInvoices(): void {
    this.loadingInvoices.set(true);
    this.invoices.set([]);
    this.selectedIds.set(new Set());
    const cid = this.companyContext.currentCompanyId();
    this.invoiceService.getList({
      companyId: cid ?? undefined,
      fromDate: this.fromDate,
      toDate: this.toDate,
      status: 'Submitted',
      skipCount: 0,
      maxResultCount: 200,
    } as any).subscribe({
      next: r => { this.invoices.set(r.items ?? []); this.loadingInvoices.set(false); },
      error: () => this.loadingInvoices.set(false),
    });
  }

  isSelected(id?: string): boolean { return this.selectedIds().has(id!); }

  toggleOne(id: string | undefined, e: Event): void {
    if (!id) return;
    const checked = (e.target as HTMLInputElement).checked;
    this.selectedIds.update(s => { const ns = new Set(s); checked ? ns.add(id) : ns.delete(id); return ns; });
  }

  toggleAll(e: Event): void {
    const checked = (e.target as HTMLInputElement).checked;
    if (checked) {
      this.selectedIds.set(new Set(this.invoices().map((i: any) => i.id)));
    } else {
      this.selectedIds.set(new Set());
    }
  }

  selectAll(): void { this.selectedIds.set(new Set(this.invoices().map((i: any) => i.id))); }
  clearAll(): void { this.selectedIds.set(new Set()); }

  eiBadge(status: string): string {
    const m: Record<string, string> = { Valid: 'bg-success', Invalid: 'bg-danger', Submitted: 'bg-primary', Cancelled: 'bg-secondary' };
    return m[status] ?? 'bg-warning text-dark';
  }

  submitBatch(): void {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) { this.toaster.error('Select a company first'); return; }
    this.submitting.set(true);
    this.results.set(null);
    this.eiService.batchSubmit({
      companyId: cid,
      sourceDocumentType: this.docType,
      documentIds: this.selected(),
    }).subscribe({
      next: r => {
        this.results.set(r);
        this.submitting.set(false);
        if ((r.succeededCount ?? 0) > 0) {
          this.toaster.success(`${r.succeededCount} document(s) submitted successfully`);
        }
        if ((r.failedCount ?? 0) > 0) {
          this.toaster.error(`${r.failedCount} document(s) failed`);
        }
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? 'Batch submit failed');
        this.submitting.set(false);
      },
    });
  }
}
