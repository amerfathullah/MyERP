import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { CompanyService } from '../../proxy/core/company.service';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { PurchaseInvoiceStore } from '../store/purchase-invoice.store';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { PurchaseInvoicePrintLayoutComponent } from '../../shared/components/purchase-invoice-print-layout/purchase-invoice-print-layout.component';
import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';
import type { PurchaseInvoiceDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-invoice-detail',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe, FormsModule, RouterLink,
    DocumentWorkflowComponent, LhdnStatusBadgeComponent, LoadingOverlayComponent, BreadcrumbComponent, ActivityLogComponent, VoucherLedgerComponent, PurchaseInvoicePrintLayoutComponent, DocumentConnectionsComponent, CompanyCurrencyPipe],
  templateUrl: './purchase-invoice-detail.component.html',
  styleUrls: ['./purchase-invoice-detail.component.scss'],
})
export class PurchaseInvoiceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private companyService = inject(CompanyService);
  private service = inject(PurchaseInvoiceService);
  private store = inject(PurchaseInvoiceStore);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private eInvoiceService = inject(EInvoiceService);
  private http = inject(HttpClient);
  private localization = inject(LocalizationService);

  invoice: PurchaseInvoiceDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];
  paymentSchedule = signal<any[]>([]);
  linkedPayments = signal<any[]>([]);
  companyData = signal<any>(null);
  supplierHoldType = signal<string | null>(null);

  // 3-Way Matching data
  matchingData = signal<any[]>([]);

  // Tax Withholding (TDS/WHT) data — Malaysia Section 107A compliance
  taxWithholdingEntries = signal<any[]>([]);

  // Quick Payment Dialog state
  showQuickPayment = signal(false);
  quickPaymentAmount = signal(0);
  quickPaymentDate = signal(new Date().toISOString().substring(0, 10));
  quickPaymentReference = signal('');
  quickPaymentMode = signal('');
  isProcessingPayment = signal(false);
  modesOfPayment = signal<any[]>([]);

  get workflowActions(): WorkflowAction[] {
    if (!this.invoice) return [];
    const actions: WorkflowAction[] = [];
    if (this.invoice.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'fa fa-paper-plane', color: 'primary' });
    }
    if (this.invoice.status === 'Submitted') {
      actions.push({ name: 'post', label: 'Post', icon: 'fa fa-check-double', color: 'success' });
    }
    if (this.invoice.status === 'Posted') {
      actions.push({ name: 'payment', label: 'Make Payment', icon: 'fa fa-money-bill', color: 'success' });
      actions.push({ name: 'return', label: 'Create Debit Note', icon: 'fa fa-rotate-left', color: 'warning' });
      if ((this.invoice as any).outstandingAmount > 0) {
        actions.push({ name: 'writeOff', label: 'Write Off', icon: 'fa fa-eraser', color: 'secondary' });
      }
      if (!this.invoice.eInvoiceStatus || this.invoice.eInvoiceStatus === 'NotSubmitted') {
        actions.push({ name: 'submitLhdn', label: 'Submit to LHDN', icon: 'fa fa-cloud-arrow-up', color: 'primary' });
      }
      if (this.invoice.eInvoiceStatus === 'Valid' || this.invoice.eInvoiceStatus === 'Submitted') {
        actions.push({ name: 'refreshLhdn', label: 'Refresh Status', icon: 'fa fa-rotate', color: 'info' });
      }
      if (this.invoice.eInvoiceStatus === 'Valid' && this.isWithin72HourWindow()) {
        actions.push({ name: 'cancelLhdn', label: 'Cancel e-Invoice', icon: 'fa fa-cloud-xmark', color: 'warning' });
      }
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa fa-ban', color: 'danger' });
    }
    if (this.invoice.status === 'Cancelled') {
      actions.push({ name: 'amend', label: 'Amend', icon: 'fa fa-file-circle-plus', color: 'success' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.invoice = result;
      this.service.getPaymentSchedule(id)
        .subscribe(schedule => this.paymentSchedule.set(schedule ?? []));
      // Load supplier hold status for payment warning
      if (result.supplierId) {
        this.http.get<any>(`/api/app/supplier/${result.supplierId}`).subscribe({
          next: (s) => this.supplierHoldType.set(s?.holdType > 0 ? this.getHoldLabel(s.holdType) : null),
          error: () => {},
        });
      }
      // Load 3-way matching data for posted invoices with PO-linked items
      if (result.status === 'Posted' || result.status === 'Submitted') {
        this.loadThreeWayMatching(id);
        this.loadTaxWithholding(id);
        this.loadLinkedPayments(id);
      }
      // Load company data for print layout
      if (result.companyId) {
        this.companyService.get(result.companyId).subscribe({
          next: (company) => this.companyData.set(company),
          error: () => {} // Non-critical for print
        });
      }
    });
  }

  /** Loads 3-way matching data from backend (PO qty/rate vs PR qty vs PI qty/rate) */
  private loadThreeWayMatching(invoiceId: string): void {
    this.http.get<any[]>(`/api/app/purchase-invoice/${invoiceId}/three-way-matching`).subscribe({
      next: (data) => this.matchingData.set(data ?? []),
      error: () => {} // Non-critical — matching is advisory
    });
  }

  /** Loads tax withholding (TDS/WHT) entries for the invoice — Malaysia Section 107A */
  private loadTaxWithholding(invoiceId: string): void {
    this.http.get<any[]>(`/api/app/purchase-invoice/${invoiceId}/tax-withholding-entries`).subscribe({
      next: (data) => this.taxWithholdingEntries.set(data ?? []),
      error: () => {}
    });
  }

  private loadLinkedPayments(invoiceId: string): void {
    this.http.get<any[]>(`/api/app/purchase-invoice/${invoiceId}/payments`).subscribe({
      next: (payments) => this.linkedPayments.set(payments ?? []),
      error: () => {}
    });
  }

  getTotalPaid(): number {
    return this.linkedPayments().reduce((sum, p) => sum + (p.paidAmount ?? 0), 0);
  }

  /** Returns true if any matching row has a discrepancy */
  hasMatchingDiscrepancy(): boolean {
    return this.matchingData().some(m => m.hasDiscrepancy);
  }

  /** Total tax withheld across all entries — displayed in TWE footer */
  getTotalWithheld(): number {
    return this.taxWithholdingEntries().reduce((sum: number, e: any) => sum + (e.withheldAmount ?? 0), 0);
  }

  onWorkflowAction(action: string): void {
    const id = this.invoice!.id!;
    switch (action) {
      case 'submit':
        this.store.submitInvoice(id);
        this.reloadAfterAction();
        break;
      case 'post':
        this.store.postInvoice(id);
        this.reloadAfterAction();
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelInvoice(id);
            this.reloadAfterAction();
          }
        });
        break;
      case 'payment':
        this.openQuickPayment();
        break;
      case 'return':
        this.router.navigate(['/purchasing/invoices/new'], {
          queryParams: { returnAgainst: id }
        });
        break;
      case 'writeOff':
        this.confirmation.warn('::WriteOffConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.service.writeOff(id).subscribe({
              next: () => { this.toaster.success('::SuccessfullyWrittenOff'); this.reloadAfterAction(); },
              error: () => {},
            });
          }
        });
        break;
      case 'amend':
        this.service.amend(id).subscribe({
          next: (amended) => this.router.navigate(['/purchasing/invoices', amended.id]),
          error: () => {},
        });
        break;
      case 'submitLhdn':
        this.submitToLhdn();
        break;
      case 'refreshLhdn':
        this.refreshLhdnStatus();
        break;
      case 'cancelLhdn':
        this.cancelLhdn();
        break;
    }
  }

  private submitToLhdn(): void {
    this.eInvoiceService.submit({
      companyId: this.invoice!.companyId!,
      sourceDocumentType: 'PurchaseInvoice',
      sourceDocumentId: this.invoice!.id!,
    }).subscribe({
      next: (submission: any) => {
        this.toaster.success('::SuccessfullySubmittedToLhdn');
        this.reloadAfterAction();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::LhdnSubmissionFailed');
      },
    });
  }

  private refreshLhdnStatus(): void {
    const submissionId = (this.invoice as any).lhdnSubmissionId ?? this.invoice!.id!;
    this.eInvoiceService.getStatus(submissionId).subscribe({
      next: () => {
        this.toaster.success('::LhdnStatusRefreshed');
        this.reloadAfterAction();
      },
      error: () => this.toaster.error('::LhdnRefreshFailed'),
    });
  }

  private cancelLhdn(): void {
    this.confirmation.warn(
      '::LhdnCancelConfirmation',
      '::AreYouSure'
    ).subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        const submissionId = (this.invoice as any).lhdnSubmissionId ?? this.invoice!.id!;
        this.eInvoiceService.cancel({ submissionId, reason: 'Cancelled by user' }).subscribe({
          next: () => {
            this.toaster.success('::LhdnInvoiceCancelled');
            this.reloadAfterAction();
          },
          error: (err: any) => this.toaster.error(err?.error?.error?.message || '::LhdnCancelFailed'),
        });
      }
    });
  }

  isWithin72HourWindow(): boolean {
    const submittedAt = (this.invoice as any)?.lhdnSubmittedAt;
    if (!submittedAt) return false;
    const hoursSince = (Date.now() - new Date(submittedAt).getTime()) / (1000 * 60 * 60);
    return hoursSince <= 72;
  }

  getLhdnQrCodeUrl(): string {
    const longId = (this.invoice as any)?.lhdnLongId;
    if (!longId) return '';
    return `https://api.qrserver.com/v1/create-qr-code/?data=https://myinvois.hasil.gov.my/${longId}/share&size=96x96`;
  }

  getLhdnVerificationUrl(): string {
    const longId = (this.invoice as any)?.lhdnLongId;
    if (!longId) return '';
    return `https://myinvois.hasil.gov.my/${longId}/share`;
  }

  // --- Quick Payment Dialog ---
  openQuickPayment(): void {
    const outstanding = this.getOutstandingAmount();
    this.quickPaymentAmount.set(outstanding);
    this.quickPaymentDate.set(new Date().toISOString().substring(0, 10));
    this.quickPaymentReference.set('');
    this.quickPaymentMode.set('');
    this.showQuickPayment.set(true);
    if (this.modesOfPayment().length === 0) {
      this.http.get<any>('/api/app/master-data/modes-of-payment').subscribe({
        next: (res) => this.modesOfPayment.set(res.items ?? res ?? []),
        error: () => {},
      });
    }
  }

  cancelQuickPayment(): void {
    this.showQuickPayment.set(false);
  }

  submitQuickPayment(): void {
    const inv = this.invoice;
    if (!inv) return;
    const amount = this.quickPaymentAmount();
    if (amount <= 0) { this.toaster.warn('::AmountMustBePositive'); return; }
    this.isProcessingPayment.set(true);
    const dto = {
      companyId: (inv as any).companyId,
      paymentType: 'Pay',
      partyType: 'Supplier',
      partyId: inv.supplierId,
      paidAmount: amount,
      postingDate: this.quickPaymentDate(),
      referenceNumber: this.quickPaymentReference() || undefined,
      modeOfPaymentId: this.quickPaymentMode() || undefined,
      againstInvoiceType: 'PurchaseInvoice',
      againstInvoiceId: inv.id,
      paidFromAccountId: undefined,
      paidToAccountId: undefined,
    };
    this.http.post<any>('/api/app/payment-entry', dto).subscribe({
      next: (pe) => {
        this.http.post(`/api/app/payment-entry/${pe.id}/submit`, {}).subscribe({
          next: () => {
            this.http.post(`/api/app/payment-entry/${pe.id}/post`, {}).subscribe({
              next: () => {
                this.isProcessingPayment.set(false);
                this.showQuickPayment.set(false);
                this.toaster.success(this.localization.instant('::PaymentReceivedSuccessfully'));
                this.reloadAfterAction();
              },
              error: () => { this.isProcessingPayment.set(false); this.showQuickPayment.set(false); this.toaster.info('::PaymentCreatedAsDraft'); this.reloadAfterAction(); },
            });
          },
          error: () => { this.isProcessingPayment.set(false); this.showQuickPayment.set(false); this.toaster.info('::PaymentCreatedAsDraft'); this.reloadAfterAction(); },
        });
      },
      error: (err) => {
        this.isProcessingPayment.set(false);
        this.toaster.error(err?.error?.error?.message || '::FailedToCreatePaymentEntry');
      },
    });
  }

  isMultiCurrency(): boolean {
    const inv = this.invoice as any;
    return inv?.exchangeRate != null && inv.exchangeRate !== 1 && inv.exchangeRate > 0;
  }

  getOutstandingAmount(): number {
    if (!this.invoice) return 0;
    const inv = this.invoice as any;
    return Math.max(0, (inv.grandTotal ?? 0) - (inv.amountPaid ?? 0) - (inv.writeOffAmount ?? 0) - (inv.totalAdvance ?? 0));
  }

  getPaymentPercent(): number {
    if (!this.invoice || !this.invoice.grandTotal || this.invoice.grandTotal <= 0) return 0;
    const paid = (this.invoice as any).amountPaid ?? 0;
    return Math.min(100, (paid / this.invoice.grandTotal) * 100);
  }

  goToFullPaymentForm(): void {
    this.showQuickPayment.set(false);
    const inv = this.invoice!;
    const outstanding = Math.max(0, inv.outstandingAmount ?? ((inv.grandTotal ?? 0) - (inv.amountPaid ?? 0)));
    this.router.navigate(['/accounting/payments/new'], {
      queryParams: {
        partyType: 'Supplier',
        partyId: inv.supplierId,
        againstInvoiceType: 'PurchaseInvoice',
        againstInvoiceId: inv.id,
        amount: outstanding > 0 ? outstanding : undefined,
        companyId: inv.companyId,
        currency: inv.currencyCode || undefined,
      }
    });
  }

  private reloadAfterAction(): void {
    const id = this.invoice!.id!;
    this.service.get(id).subscribe({
      next: (r) => { this.invoice = r; this.loadLinkedPayments(id); },
      error: () => {}
    });
  }

  printInvoice(): void {
    window.print();
  }

  amend(): void {
    this.service.amend(this.invoice!.id!).subscribe({
      next: (amended) => {
        this.router.navigate(['/purchasing/invoices', amended.id]);
      },
    });
  }

  deleteInvoice(): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(this.invoice!.id!).subscribe({
        next: () => this.router.navigate(['/purchasing/invoices']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    });
  }

  // --- Early Payment Discount Helpers ---

  isTermOverdue(entry: any): boolean {
    if (!entry.dueDate || entry.outstanding <= 0.01) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const dueDate = new Date(entry.dueDate);
    dueDate.setHours(0, 0, 0, 0);
    return dueDate < today;
  }

  hasActiveDiscount(entry: any): boolean {
    if (!entry.discountType || entry.discountPercentage <= 0 || entry.outstanding <= 0.01) return false;
    if (!entry.discountValidTill) return true;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const validTill = new Date(entry.discountValidTill);
    validTill.setHours(0, 0, 0, 0);
    return today <= validTill;
  }

  isDiscountExpired(entry: any): boolean {
    if (!entry.discountValidTill) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const validTill = new Date(entry.discountValidTill);
    validTill.setHours(0, 0, 0, 0);
    return today > validTill;
  }

  getDiscountSaving(entry: any): number {
    if (!entry.discountType || entry.discountPercentage <= 0) return 0;
    if (entry.discountType === 'Percentage') {
      return entry.paymentAmount * entry.discountPercentage / 100;
    }
    return entry.discountPercentage;
  }

  getTotalDiscountSavings(): number {
    return this.paymentSchedule()
      .filter(e => this.hasActiveDiscount(e))
      .reduce((sum, e) => sum + this.getDiscountSaving(e), 0);
  }

  private getHoldLabel(holdType: number): string {
    switch (holdType) {
      case 1: return 'All Transactions';
      case 2: return 'Invoices';
      case 3: return 'Payments';
      default: return 'On Hold';
    }
  }
}
