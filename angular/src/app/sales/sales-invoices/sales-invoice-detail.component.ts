import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DocumentPrintService } from '../../proxy/core/document-print.service';
import { MasterDataService } from '../../proxy/core/master-data.service';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';
import { FormsModule } from '@angular/forms';
import { CompanyService } from '../../proxy/core/company.service';
import { PageModule } from '@abp/ng.components/page';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { SalesInvoiceStore } from '../store/sales-invoice.store';
import type { SalesInvoiceDto } from '../../proxy/sales/models';

export interface DetailWorkflowAction {
  name: string;
  label: string;
  icon: string;
  btnClass: string;
}

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { InvoicePrintLayoutComponent } from '../../shared/components/invoice-print-layout/invoice-print-layout.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';
import { downloadPdfFromResult } from '../../shared/utils/pdf-download.util';

@Component({
  selector: 'app-sales-invoice-detail',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent,
    ActivityLogComponent,
    BreadcrumbComponent,
    InvoicePrintLayoutComponent,
    VoucherLedgerComponent,
    DocumentConnectionsComponent,
    CompanyCurrencyPipe,
    FormsModule,
    RouterLink,
    LocalizationPipe],
  templateUrl: './sales-invoice-detail.component.html',
  styleUrls: ['./sales-invoice-detail.component.scss'],
})
export class SalesInvoiceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private companyService = inject(CompanyService);
  private service = inject(SalesInvoiceService);
  private eInvoiceService = inject(EInvoiceService);
  private store = inject(SalesInvoiceStore);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private documentPrintService = inject(DocumentPrintService);
  private masterDataService = inject(MasterDataService);
  private paymentEntryService = inject(PaymentEntryService);
  private documentEmailService = inject(DocumentEmailService);
  private localization = inject(LocalizationService);

  invoice: SalesInvoiceDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];
  paymentSchedule = signal<any[]>([]);
  linkedPayments = signal<any[]>([]);
  companyData = signal<any>(null);

  // Quick Payment Dialog state
  showQuickPayment = signal(false);
  quickPaymentAmount = signal(0);
  quickPaymentDate = signal(new Date().toISOString().substring(0, 10));
  quickPaymentReference = signal('');
  quickPaymentMode = signal('');
  isProcessingPayment = signal(false);
  modesOfPayment = signal<any[]>([]);

  get workflowActions(): DetailWorkflowAction[] {
    if (!this.invoice) return [];
    const actions: DetailWorkflowAction[] = [];
    switch (this.invoice.status) {
      case 'Draft':
        actions.push({ name: 'submit', label: 'Submit', icon: 'fa fa-paper-plane', btnClass: 'btn-primary' });
        break;
      case 'Submitted':
        actions.push({ name: 'post', label: 'Post', icon: 'fa fa-check-double', btnClass: 'btn-success' });
        break;
      case 'Posted':
        actions.push({ name: 'payment', label: 'Make Payment', icon: 'fa fa-money-bill', btnClass: 'btn-success' });
        // Per ERPNext: SI→DN enables goods delivery from an already-posted invoice (service→goods flow)
        if (!(this.invoice as any).updateStock && !(this.invoice as any).isReturn) {
          actions.push({ name: 'createDN', label: 'Create Delivery Note', icon: 'fa fa-truck', btnClass: 'btn-outline-info' });
        }
        actions.push({ name: 'return', label: 'Create Return', icon: 'fa fa-rotate-left', btnClass: 'btn-outline-warning' });
        if ((this.invoice as any).outstandingAmount > 0) {
          actions.push({ name: 'writeOff', label: 'Write Off', icon: 'fa fa-eraser', btnClass: 'btn-outline-secondary' });
        }
        actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa fa-ban', btnClass: 'btn-outline-danger' });
        if (!this.invoice.eInvoiceStatus || this.invoice.eInvoiceStatus === 'NotSubmitted') {
          actions.push({ name: 'submitLhdn', label: 'Submit to LHDN', icon: 'fa fa-cloud-arrow-up', btnClass: 'btn-outline-primary' });
        }
        if (this.invoice.eInvoiceStatus === 'Valid') {
          actions.push({ name: 'cancelLhdn', label: 'Cancel e-Invoice', icon: 'fa fa-cloud-xmark', btnClass: 'btn-outline-warning' });
        }
        actions.push({ name: 'sendEmail', label: 'Send Email', icon: 'fa fa-envelope', btnClass: 'btn-outline-secondary' });
        actions.push({ name: 'setRecurring', label: 'Set Recurring', icon: 'fa fa-repeat', btnClass: 'btn-outline-info' });
        break;
      case 'Cancelled':
        actions.push({ name: 'amend', label: 'Amend', icon: 'fa fa-file-circle-plus', btnClass: 'btn-outline-success' });
        break;
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.invoice = result;
      // Load payment schedule
      this.service.getPaymentSchedule(id)
        .subscribe(schedule => this.paymentSchedule.set(schedule ?? []));
      // Load linked payments for posted invoices
      if (result.status === 'Posted' || result.status === 'Submitted') {
        this.loadLinkedPayments(id);
      }
      // Load company data for print layout
      if ((result as any).companyId) {
        this.companyService.get((result as any).companyId).subscribe({
          next: (company) => this.companyData.set(company),
          error: () => {}
        });
      }
    });
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
        this.confirmation.warn('::Sales:CancelInvoiceConfirmation', '::AreYouSure').subscribe((status) => {
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
        this.router.navigate(['/sales/invoices/new'], {
          queryParams: { returnAgainst: id }
        });
        break;
      case 'submitLhdn':
        this.submitToLhdn();
        break;
      case 'cancelLhdn':
        this.cancelLhdn();
        break;
      case 'writeOff':
        this.confirmation.warn('::WriteOffConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.service.writeOff(id).subscribe({
              next: () => { this.toaster.success('::SuccessfullyUpdated'); this.reloadAfterAction(); },
              error: () => {},
            });
          }
        });
        break;
      case 'amend':
        this.service.amend(id).subscribe({
          next: (amended) => this.router.navigate(['/sales/invoices', amended.id]),
          error: () => {},
        });
        break;
      case 'createDN':
        // Navigate to DN form with pre-fill from this SI
        this.router.navigate(['/sales/delivery-notes/new'], {
          queryParams: { salesInvoiceId: id, customerId: this.invoice!.customerId }
        });
        break;
      case 'sendEmail':
        this.openSendEmailDialog();
        break;
      case 'setRecurring':
        this.router.navigate(['/settings/auto-repeat'], {
          queryParams: {
            docType: 'SalesInvoice',
            docId: id,
            docNumber: this.invoice!.invoiceNumber
          }
        });
        break;
    }
  }

  private submitToLhdn(): void {
    this.eInvoiceService.submit({
      companyId: this.invoice!.companyId!,
      sourceDocumentType: 'SalesInvoice',
      sourceDocumentId: this.invoice!.id!,
    }).subscribe({
      next: (submission) => {
        this.toaster.success('::LhdnSubmissionSuccessful');
        this.reloadAfterAction();
      },
      error: (err) => {
        this.toaster.error(err?.error?.error?.message ?? '::LhdnSubmissionFailed');
      },
    });
  }

  private cancelLhdn(): void {
    this.confirmation.warn(
      'Cancel this e-Invoice with LHDN? This is only allowed within 72 hours of submission.',
      'MyERP::AreYouSure'
    ).subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.eInvoiceService.cancel({
          submissionId: (this.invoice as any).eInvoiceSubmissionId ?? this.invoice!.id!,
          reason: 'Cancelled by user',
        }).subscribe({
          next: () => {
            this.toaster.success('::LhdnInvoiceCancelled');
            this.reloadAfterAction();
          },
          error: (err) => {
            this.toaster.error(err?.error?.error?.message || '::LhdnCancelFailed');
          },
        });
      }
    });
  }

  edit(): void {
    this.router.navigate(['/sales/invoices', this.invoice!.id, 'edit']);
  }

  goBack(): void {
    this.router.navigate(['/sales/invoices']);
  }

  printInvoice(): void {
    window.print();
  }

  downloadPdf(): void {
    this.documentPrintService.getSalesInvoicePrint(this.invoice!.id!)
      .subscribe({
        next: (result) => {
          if (!downloadPdfFromResult(result, 'invoice.pdf')) {
            this.toaster.error('::OperationFailed');
          }
        },
        error: () => this.toaster.error('::OperationFailed'),
      });
  }

  duplicate(): void {
    this.router.navigate(['/sales/invoices/new'], {
      queryParams: { duplicateFrom: this.invoice!.id }
    });
  }

  amend(): void {
    this.service.amend(this.invoice!.id!).subscribe({
      next: (amended) => {
        this.router.navigate(['/sales/invoices', amended.id]);
      },
    });
  }

  // --- Quick Payment Dialog ---
  openQuickPayment(): void {
    const outstanding = this.getOutstandingAmount();
    this.quickPaymentAmount.set(outstanding);
    this.quickPaymentDate.set(new Date().toISOString().substring(0, 10));
    this.quickPaymentReference.set('');
    this.quickPaymentMode.set('');
    this.showQuickPayment.set(true);
    // Load modes of payment if not yet loaded
    if (this.modesOfPayment().length === 0) {
      this.masterDataService.getModesOfPayment().subscribe({
        next: (res: any) => this.modesOfPayment.set(res.items ?? res ?? []),
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
    if (amount <= 0) {
      this.toaster.warn('::AmountMustBePositive');
      return;
    }
    this.isProcessingPayment.set(true);
    const dto = {
      companyId: (inv as any).companyId,
      paymentType: 'Receive',
      partyType: 'Customer',
      partyId: inv.customerId,
      paidAmount: amount,
      postingDate: this.quickPaymentDate(),
      referenceNumber: this.quickPaymentReference() || undefined,
      modeOfPaymentId: this.quickPaymentMode() || undefined,
      againstInvoiceType: 'SalesInvoice',
      againstInvoiceId: inv.id,
      paidFromAccountId: undefined,
      paidToAccountId: undefined,
    };
    this.paymentEntryService.create(dto as any).subscribe({
      next: (pe: any) => {
        this.paymentEntryService.submit(pe.id).subscribe({
          next: () => {
            this.paymentEntryService.post(pe.id).subscribe({
              next: () => {
                this.isProcessingPayment.set(false);
                this.showQuickPayment.set(false);
                this.toaster.success(this.localization.instant('::PaymentReceivedSuccessfully'));
                this.reloadAfterAction();
              },
              error: () => {
                this.isProcessingPayment.set(false);
                this.showQuickPayment.set(false);
                this.toaster.info('::PaymentCreatedAsDraft');
                this.reloadAfterAction();
              },
            });
          },
          error: () => {
            this.isProcessingPayment.set(false);
            this.showQuickPayment.set(false);
            this.toaster.info('::PaymentCreatedAsDraft');
            this.reloadAfterAction();
          },
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
    const outstanding = (inv.grandTotal ?? 0) - (inv.amountPaid ?? 0) - (inv.writeOffAmount ?? 0) - (inv.totalAdvance ?? 0);
    return Math.max(0, outstanding);
  }

  getPaymentPercent(): number {
    if (!this.invoice || !this.invoice.grandTotal || this.invoice.grandTotal <= 0) return 0;
    const paid = (this.invoice as any).amountPaid ?? 0;
    return Math.min(100, (paid / this.invoice.grandTotal) * 100);
  }

  /** LHDN QR code URL generated from longId (per myinvois get_status.py pattern) */
  getLhdnQrCodeUrl(): string {
    const inv = this.invoice as any;
    if (inv?.qrCodeUrl) return inv.qrCodeUrl;
    if (inv?.lhdnLongId) {
      const verifyUrl = `https://myinvois.hasil.gov.my/${inv.lhdnLongId}/share`;
      return `https://api.qrserver.com/v1/create-qr-code/?size=96x96&data=${encodeURIComponent(verifyUrl)}`;
    }
    return '';
  }

  /** LHDN MyInvois verification portal URL */
  getLhdnVerificationUrl(): string {
    const inv = this.invoice as any;
    if (inv?.lhdnLongId) {
      return `https://myinvois.hasil.gov.my/${inv.lhdnLongId}/share`;
    }
    return '';
  }

  goToFullPaymentForm(): void {
    this.showQuickPayment.set(false);
    const inv = this.invoice!;
    const outstanding = Math.max(0, inv.outstandingAmount ?? ((inv.grandTotal ?? 0) - (inv.amountPaid ?? 0)));
    this.router.navigate(['/accounting/payments/new'], {
      queryParams: {
        partyType: 'Customer',
        partyId: inv.customerId,
        againstInvoiceType: 'SalesInvoice',
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
      next: (result) => {
        this.invoice = result;
        this.loadLinkedPayments(id);
      },
      error: () => {}
    });
  }

  private loadLinkedPayments(invoiceId: string): void {
    this.service.getPayments(invoiceId).subscribe({
      next: (payments: any) => this.linkedPayments.set(payments ?? []),
      error: () => {}
    });
  }

  getTotalPaid(): number {
    return this.linkedPayments().reduce((sum, p) => sum + (p.paidAmount ?? 0), 0);
  }

  deleteInvoice(): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(this.invoice!.id!).subscribe({
        next: () => this.router.navigate(['/sales/invoices']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    });
  }

  isTermOverdue(entry: any): boolean {
    if (!entry.dueDate || entry.outstanding <= 0.01) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const dueDate = new Date(entry.dueDate);
    dueDate.setHours(0, 0, 0, 0);
    return dueDate < today;
  }

  /** Whether the entry has an active (non-expired) early payment discount. */
  hasActiveDiscount(entry: any): boolean {
    if (!entry.discountType || entry.discountPercentage <= 0 || entry.outstanding <= 0.01) return false;
    if (!entry.discountValidTill) return true; // No expiry = always available
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const validTill = new Date(entry.discountValidTill);
    validTill.setHours(0, 0, 0, 0);
    return today <= validTill;
  }

  /** Whether the entry's discount has expired (had a discount but window passed). */
  isDiscountExpired(entry: any): boolean {
    if (!entry.discountValidTill) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const validTill = new Date(entry.discountValidTill);
    validTill.setHours(0, 0, 0, 0);
    return today > validTill;
  }

  /** Calculate the discount saving for a single payment term entry. */
  getDiscountSaving(entry: any): number {
    if (!entry.discountType || entry.discountPercentage <= 0) return 0;
    if (entry.discountType === 'Percentage') {
      return entry.paymentAmount * entry.discountPercentage / 100;
    }
    return entry.discountPercentage; // Fixed amount discount
  }

  /** Total savings available across all payment schedule entries with active discounts. */
  getTotalDiscountSavings(): number {
    return this.paymentSchedule()
      .filter(e => this.hasActiveDiscount(e))
      .reduce((sum, e) => sum + this.getDiscountSaving(e), 0);
  }

  // --- Send Email Dialog ---
  showEmailDialog = false;
  emailRecipient = '';
  emailCc = '';
  emailAttachPdf = true;
  emailSending = false;

  openSendEmailDialog(): void {
    // Pre-fill with customer email
    this.emailRecipient = (this.invoice as any)?.customerEmail || '';
    this.emailCc = '';
    this.emailAttachPdf = true;
    this.showEmailDialog = true;
  }

  sendEmail(): void {
    if (!this.emailRecipient) {
      this.toaster.warn('::PleaseEnterRecipientEmail');
      return;
    }
    this.emailSending = true;
    const payload = {
      invoiceId: this.invoice!.id,
      recipientEmail: this.emailRecipient,
      ccEmails: this.emailCc ? this.emailCc.split(',').map((e: string) => e.trim()) : null,
      attachPdf: this.emailAttachPdf,
    };
    this.documentEmailService.sendSalesInvoiceEmail(payload as any).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySent');
        this.showEmailDialog = false;
        this.emailSending = false;
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::FailedToSendEmail');
        this.emailSending = false;
      },
    });
  }

  cancelEmailDialog(): void {
    this.showEmailDialog = false;
  }

  hasGrossProfitData(): boolean {
    return (this.invoice?.items ?? []).some((i: any) => i.valuationRate > 0);
  }

  getItemMarginPct(item: any): number {
    if (!item.unitPrice || item.unitPrice <= 0) return 0;
    return ((item.unitPrice - (item.valuationRate ?? 0)) / item.unitPrice) * 100;
  }

  getTotalGrossProfit(): number {
    return (this.invoice?.items ?? []).reduce((sum: number, i: any) =>
      sum + ((i.unitPrice ?? 0) - (i.valuationRate ?? 0)) * (i.quantity ?? 0), 0);
  }

  getOverallMarginPct(): number {
    const net = this.invoice?.netTotal ?? 0;
    if (net <= 0) return 0;
    return (this.getTotalGrossProfit() / net) * 100;
  }
}
