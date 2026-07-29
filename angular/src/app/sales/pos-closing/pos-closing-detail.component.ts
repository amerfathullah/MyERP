import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { PosClosingService } from '../../proxy/sales/pos-closing.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';

@Component({
  selector: 'app-pos-closing-detail',
  standalone: true,
  imports: [CommonModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent, VoucherLedgerComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid py-3">
      @if (!entry()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h4 class="mb-0">
            <i class="fa fa-register me-2 text-primary"></i>
            {{ '::PosClosingEntry' | abpLocalization }}
          </h4>
          <div class="btn-group">
            @if (entry()!.status === 'Draft') {
              <button class="btn btn-outline-success btn-sm" (click)="submit()">
                <i class="fa fa-check me-1"></i>{{ '::Submit' | abpLocalization }}
              </button>
            }
            @if (entry()!.status === 'Submitted') {
              <button class="btn btn-outline-danger btn-sm" (click)="cancelEntry()">
                <i class="fa fa-ban me-1"></i>{{ '::Cancel' | abpLocalization }}
              </button>
            }
            @if (entry()!.status === 'Failed') {
              <button class="btn btn-outline-primary btn-sm" (click)="retry()">
                <i class="fa fa-rotate-right me-1"></i>{{ '::Retry' | abpLocalization }}
              </button>
              <button class="btn btn-outline-danger btn-sm" (click)="cancelEntry()">
                <i class="fa fa-ban me-1"></i>{{ '::Cancel' | abpLocalization }}
              </button>
            }
            <button class="btn btn-outline-secondary btn-sm" (click)="goBack()">
              <i class="fa fa-arrow-left me-1"></i>{{ '::Back' | abpLocalization }}
            </button>
          </div>
        </div>

        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::Status' | abpLocalization }}</div>
              <div class="mt-1"><app-status-badge [status]="entry()!.status || 'Draft'" /></div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::PostingDate' | abpLocalization }}</div>
              <div class="fw-bold mt-1">{{ entry()!.postingDate | date:'dd/MM/yyyy HH:mm' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::GrandTotal' | abpLocalization }}</div>
              <div class="fw-bold mt-1 text-primary fs-5">{{ entry()!.grandTotal | number:'1.2-2' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::TotalDifference' | abpLocalization }}</div>
              <div class="fw-bold mt-1" [class.text-success]="(entry()!.totalDifference || 0) >= 0"
                [class.text-danger]="(entry()!.totalDifference || 0) < 0">
                {{ entry()!.totalDifference | number:'1.2-2' }}
              </div>
            </div>
          </div>
        </div>

        <!-- Payment Reconciliation -->
        <div class="card shadow-sm mb-3">
          <div class="card-header"><h6 class="mb-0">{{ '::PaymentReconciliation' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::PaymentMode' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ExpectedAmount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ClosingAmount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Difference' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (p of entry()!.payments || []; track p.modeOfPayment) {
                  <tr>
                    <td><i class="fa fa-credit-card me-2 text-muted"></i>{{ p.modeOfPayment }}</td>
                    <td class="text-end">{{ p.expectedAmount | number:'1.2-2' }}</td>
                    <td class="text-end">{{ p.closingAmount | number:'1.2-2' }}</td>
                    <td class="text-end" [class.text-success]="(p.difference || 0) >= 0"
                      [class.text-danger]="(p.difference || 0) < 0">
                      {{ p.difference | number:'1.2-2' }}
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="4" class="text-center text-muted py-3">{{ '::NoPayments' | abpLocalization }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <!-- Linked Invoices -->
        <div class="card shadow-sm mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ '::LinkedInvoices' | abpLocalization }}</h6>
            <span class="badge bg-primary">{{ entry()!.invoices?.length || 0 }}</span>
          </div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::InvoiceNumber' | abpLocalization }}</th>
                  <th class="text-end">{{ '::GrandTotal' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (inv of entry()!.invoices || []; track inv.salesInvoiceId) {
                  <tr>
                    <td>
                      <a [routerLink]="['/sales/invoices', inv.salesInvoiceId]">
                        {{ inv.invoiceNumber || inv.salesInvoiceId || '—' }}
                      </a>
                    </td>
                    <td class="text-end">{{ inv.grandTotal | number:'1.2-2' }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="2" class="text-center text-muted py-3">{{ '::NoInvoicesLinked' | abpLocalization }}</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        @if (entry()!.consolidatedSalesInvoiceId) {
          <div class="alert alert-success">
            <i class="fa fa-check-circle me-2"></i>
            {{ '::ConsolidatedInto' | abpLocalization }}:
            <a [routerLink]="['/sales/invoices', entry()!.consolidatedSalesInvoiceId]" class="alert-link">
              {{ '::ViewConsolidatedInvoice' | abpLocalization }}
            </a>
          </div>
        }

        @if (entry()!.status === 'Submitted' || entry()!.status === 'Cancelled') {
          <app-voucher-ledger
            voucherType="PosClosingEntry"
            [voucherId]="entryId"
            [showStock]="false"
            [showAccounting]="true" />
        }
        <app-activity-log documentType="PosClosingEntry" [documentId]="entryId" />
      }
    </div>
  `,
})
export class PosClosingDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private router = inject(Router);
  private service = inject(PosClosingService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);

  entry = signal<any>(null);
  entryId = '';

  ngOnInit() {
    this.entryId = this.route.snapshot.params['id'];
    this.loadEntry();
  }

  loadEntry() {
    this.service.get(this.entryId).subscribe({
      next: (data) => this.entry.set(data),
      error: () => this.toaster.error(this.l('::FailedToLoad')),
    });
  }

  submit() {
    this.confirmation.warn('::PosClosingSubmitConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.submit(this.entryId).subscribe({
        next: () => {
          this.toaster.success(this.l('::SuccessfullySubmitted'));
          this.loadEntry();
        },
        error: () => {},
      });
    });
  }

  cancelEntry() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(this.entryId).subscribe({
        next: () => {
          this.toaster.success(this.l('::SuccessfullyCancelled'));
          this.loadEntry();
        },
        error: () => {},
      });
    });
  }

  retry() {
    this.service.retry(this.entryId).subscribe({
      next: () => {
        this.toaster.success(this.l('::SuccessfullySubmitted'));
        this.loadEntry();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  goBack() {
    this.router.navigate(['/sales/pos-closing']);
  }

  private l(key: string): string { return this.localization.instant(key); }
}
