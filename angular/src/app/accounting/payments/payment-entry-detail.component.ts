import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { HttpClient } from '@angular/common/http';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import { PaymentEntryPrintLayoutComponent } from '../../shared/components/pe-print-layout/pe-print-layout.component';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';

@Component({
  selector: 'app-payment-entry-detail',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, DocumentWorkflowComponent, ActivityLogComponent, VoucherLedgerComponent, RouterLink, PaymentEntryPrintLayoutComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="entry()?.paymentNumber || ('PaymentEntry' | abpLocalization)">
      @if (!entry()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="d-flex justify-content-between align-items-center mb-2">
          <div></div>
          @if (entry()!.status === 'Draft') {
            <div class="btn-group btn-group-sm">
              <a class="btn btn-outline-primary" [routerLink]="['/accounting/payments', entry()!.id, 'edit']">
                <i class="fa fa-edit me-1"></i>{{ 'Edit' | abpLocalization }}
              </a>
              <button class="btn btn-outline-danger" (click)="deleteEntry()">
                <i class="fa fa-trash me-1"></i>{{ 'Delete' | abpLocalization }}
              </button>
            </div>
          }
          @if (entry()!.status === 'Posted') {
            <button class="btn btn-sm btn-outline-secondary" (click)="printReceipt()">
              <i class="fa fa-print me-1"></i>{{ 'Print' | abpLocalization }}
            </button>
          }
        </div>
        <app-document-workflow [actions]="workflowActions" (actionClicked)="onAction($event)" />

        <div class="row mb-4">
          <div class="col-md-6">
            <div class="card">
              <div class="card-header"><h6 class="card-title mb-0">{{ 'PaymentDetails' | abpLocalization }}</h6></div>
              <div class="card-body">
                <table class="table table-borderless mb-0">
                  <tr><td class="text-muted" style="width:40%">{{ 'PaymentNumber' | abpLocalization }}</td><td class="fw-bold">{{ entry()!.paymentNumber }}</td></tr>
                  <tr><td class="text-muted">{{ 'Status' | abpLocalization }}</td><td><app-status-badge [status]="entry()!.status" /></td></tr>
                  <tr><td class="text-muted">{{ 'PaymentType' | abpLocalization }}</td><td>
                    <span class="badge" [class.bg-success]="entry()!.paymentType === 'Receive'" [class.bg-primary]="entry()!.paymentType === 'Pay'" [class.bg-info]="entry()!.paymentType === 'InternalTransfer'">
                      {{ entry()!.paymentType }}
                    </span>
                  </td></tr>
                  <tr><td class="text-muted">{{ 'PostingDate' | abpLocalization }}</td><td>{{ entry()!.postingDate | date:'dd/MM/yyyy' }}</td></tr>
                </table>
              </div>
            </div>
          </div>
          <div class="col-md-6">
            <div class="card">
              <div class="card-header"><h6 class="card-title mb-0">{{ 'Amount' | abpLocalization }}</h6></div>
              <div class="card-body">
                <div class="text-center">
                  <div class="text-muted small">{{ 'PaidAmount' | abpLocalization }}</div>
                  <div class="fs-2 fw-bold text-primary">
                    {{ entry()!.currencyCode }} {{ entry()!.paidAmount | number:'1.2-2' }}
                  </div>
                </div>
                <hr />
                <table class="table table-borderless table-sm mb-0">
                  <tr><td class="text-muted">{{ 'ModeOfPayment' | abpLocalization }}</td><td>{{ entry()!.modeOfPayment || '-' }}</td></tr>
                  <tr><td class="text-muted">{{ 'ReferenceNumber' | abpLocalization }}</td><td>{{ entry()!.referenceNumber || '-' }}</td></tr>
                </table>
              </div>
            </div>
          </div>
        </div>

        <!-- References Table (if present) -->
        @if (references().length > 0) {
          <div class="card mb-4">
            <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-link me-2"></i>{{ 'References' | abpLocalization }}</h6></div>
            <div class="card-body p-0">
              <table class="table table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ 'ReferenceType' | abpLocalization }}</th>
                    <th>{{ 'ReferenceNumber' | abpLocalization }}</th>
                    <th class="text-end">{{ 'TotalAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Outstanding' | abpLocalization }}</th>
                    <th class="text-end">{{ 'AllocatedAmount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (ref of references(); track $index) {
                    <tr>
                      <td><span class="badge bg-info-subtle text-info">{{ ref.referenceType }}</span></td>
                      <td>
                        @if (getRefRoute(ref)) {
                          <a [routerLink]="getRefRoute(ref)" class="text-primary">{{ ref.referenceNumber || ref.referenceId?.substring(0, 8) }}</a>
                        } @else {
                          {{ ref.referenceNumber || ref.referenceId?.substring(0, 8) }}
                        }
                      </td>
                      <td class="text-end">{{ ref.totalAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ ref.outstandingAmount | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold">{{ ref.allocatedAmount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <!-- Taxes Table (if present) -->
        @if (taxes().length > 0) {
          <div class="card mb-4">
            <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-percent me-2"></i>{{ 'Taxes' | abpLocalization }}</h6></div>
            <div class="card-body p-0">
              <table class="table table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Description' | abpLocalization }}</th>
                    <th>{{ 'ChargeType' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end">{{ 'TaxAmount' | abpLocalization }}</th>
                    <th class="text-center">{{ 'Included' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (tax of taxes(); track $index) {
                    <tr>
                      <td>{{ tax.description || '-' }}</td>
                      <td><span class="badge bg-secondary">{{ tax.chargeType }}</span></td>
                      <td class="text-end">{{ tax.rate | number:'1.2-2' }}%</td>
                      <td class="text-end fw-bold">{{ tax.taxAmount | number:'1.2-2' }}</td>
                      <td class="text-center">
                        @if (tax.includedInPaidAmount) {
                          <i class="fa fa-check text-success"></i>
                        } @else {
                          <i class="fa fa-minus text-muted"></i>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="table-light">
                    <td colspan="3" class="text-end fw-bold">{{ 'Total' | abpLocalization }}</td>
                    <td class="text-end fw-bold">{{ totalTaxes() | number:'1.2-2' }}</td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        }

        <app-activity-log documentType="PaymentEntry" [documentId]="entry()!.id" />

        <!-- Ledger Views (GL entries for posted payments) -->
        @if (entry()!.status === 'Posted') {
          <app-voucher-ledger
            [voucherType]="'PaymentEntry'"
            [voucherId]="entry()!.id!"
            [companyId]="entry()!.companyId!" />
        }

        <!-- Print Layout (hidden on screen, rendered on print) -->
        <app-pe-print-layout
          [payment]="entry()"
          [companyName]="companyData().name"
          [companyTin]="companyData().tin"
          [companySst]="companyData().sst"
          [companyAddress]="companyData().address"
          [companyPhone]="companyData().phone"
          [partyAddress]="''"
          [modeOfPayment]="entry()!.modeOfPayment || ''"
          [paidFromAccountName]="''"
          [paidToAccountName]="''"
          [againstInvoiceNumber]="''"
          [amountInWords]="''"
        />
      }
    </abp-page>
  `
})
export class PaymentEntryDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private paymentEntryService = inject(PaymentEntryService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private http = inject(HttpClient);

  entry = signal<any>(null);
  references = signal<any[]>([]);
  taxes = signal<any[]>([]);
  companyData = signal<{ name: string; tin: string; sst: string; address: string; phone: string }>({ name: '', tin: '', sst: '', address: '', phone: '' });
  totalTaxes = computed(() => this.taxes().reduce((sum: number, t: any) => sum + (t.taxAmount ?? 0), 0));

  get workflowActions(): WorkflowAction[] {
    const e = this.entry();
    if (!e) return [];
    const actions: WorkflowAction[] = [];
    if (e.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (e.status === 'Submitted') {
      actions.push({ name: 'post', label: 'Post', icon: 'check-double', color: 'success' });
    }
    if (e.status === 'Posted') {
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.paymentEntryService.get(id).subscribe(data => {
      this.entry.set(data);
      this.references.set((data as any).references || []);
      this.taxes.set((data as any).taxes || []);
      // Load company data for print layout
      if ((data as any).companyId) {
        this.http.get<any>(`/api/app/company/${(data as any).companyId}`).subscribe({
          next: (c) => this.companyData.set({ name: c.name || '', tin: c.tin || '', sst: c.sstRegistrationNumber || '', address: c.address || '', phone: c.phone || '' }),
          error: () => {},
        });
      }
    });
  }

  onAction(action: string): void {
    const id = this.entry()!.id;
    switch (action) {
      case 'submit':
        this.paymentEntryService.submit(id).subscribe({ next: () => this.reload(), error: () => {} });
        break;
      case 'post':
        this.paymentEntryService.post(id).subscribe({ next: () => { this.toaster.success('Payment posted.'); this.reload(); }, error: () => {} });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(s => {
          if (s === Confirmation.Status.confirm) {
            this.paymentEntryService.cancel(id).subscribe({ next: () => this.reload(), error: () => {} });
          }
        });
        break;
    }
  }

  private reload(): void {
    setTimeout(() => {
      const id = this.route.snapshot.paramMap.get('id')!;
      this.paymentEntryService.get(id).subscribe(data => {
        this.entry.set(data);
        this.references.set((data as any).references || []);
      });
    }, 500);
  }

  deleteEntry(): void {
    if (!confirm('Are you sure you want to delete this draft payment entry?')) return;
    this.paymentEntryService.delete(this.entry()!.id).subscribe({
      next: () => this.router.navigate(['/accounting/payments']),
      error: () => {},
    });
  }

  printReceipt(): void {
    window.print();
  }

  getRefRoute(ref: any): string[] | null {
    const routeMap: Record<string, string> = {
      'SalesInvoice': '/sales/invoices',
      'PurchaseInvoice': '/purchasing/invoices',
      'SalesOrder': '/sales/orders',
      'PurchaseOrder': '/purchasing/orders',
    };
    const base = routeMap[ref.referenceType];
    if (base && ref.referenceId) return [base, ref.referenceId];
    return null;
  }
}
