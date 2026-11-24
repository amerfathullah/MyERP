import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CompanyService } from '../../proxy/core/company.service';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { QuotationService } from '../../proxy/sales/quotation.service';
import { QuotationLostReasonService } from '../../proxy/sales/quotation-lost-reason.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { QuotationStore } from '../store/quotation.store';
import type { QuotationDto, QuotationLostReasonDto } from '../../proxy/sales/models';
import { CompetitorService } from '../../proxy/crm/competitor.service';
import type { CompetitorDto } from '../../proxy/crm/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { QuotationPrintLayoutComponent } from '../../shared/components/quotation-print-layout/quotation-print-layout.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';
import { DocumentPrintService } from '../../proxy/core/document-print.service';
import { downloadPdfFromResult } from '../../shared/utils/pdf-download.util';

@Component({
  selector: 'app-quotation-detail',
  standalone: true,
  imports: [
    BreadcrumbComponent, CommonModule, FormsModule, RouterModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe, ActivityLogComponent, QuotationPrintLayoutComponent, DocumentConnectionsComponent, CompanyCurrencyPipe],
  templateUrl: './quotation-detail.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./quotation-detail.component.scss'],
})
export class QuotationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private companyService = inject(CompanyService);
  private service = inject(QuotationService);
  private conversionService = inject(DocumentConversionService);
  private store = inject(QuotationStore);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  private emailService = inject(DocumentEmailService);
  private competitorService = inject(CompetitorService);
  private lostReasonService = inject(QuotationLostReasonService);
  private documentPrintService = inject(DocumentPrintService);

  quotation: QuotationDto | null = null;
  companyData = signal<any>(null);
  competitors = signal<CompetitorDto[]>([]);
  lostReasons = signal<QuotationLostReasonDto[]>([]);
  selectedCompetitorId = '';
  selectedLostReasonId = '';
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  showEmailDialog = false;
  emailRecipient = '';
  emailCc = '';
  emailAttachPdf = true;
  emailSending = false;

  get workflowActions(): WorkflowAction[] {
    if (!this.quotation) return [];
    const actions: WorkflowAction[] = [];
    if (this.quotation.status === 'Draft') {
      actions.push({ name: 'submit', label: '::Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (this.quotation.status === 'Submitted') {
      const isOrdered = this.quotation.orderStatus === 'Ordered' || (this.quotation.perOrdered ?? 0) >= 100 || !!this.quotation.convertedToSalesOrderId;
      const isPartiallyOrdered = this.quotation.orderStatus === 'Partially Ordered' || (this.quotation.perOrdered ?? 0) > 0;
      if (!isOrdered) {
        actions.push({ name: 'convert', label: '::ConvertToSO', icon: 'arrow-right-arrow-left', color: 'success' });
        actions.push({ name: 'convertToInvoice', label: '::ConvertToInvoice', icon: 'file-invoice-dollar', color: 'info' });
      }
      actions.push({ name: 'sendEmail', label: '::SendEmail', icon: 'envelope', color: 'secondary' });
      if (!isOrdered && !isPartiallyOrdered) {
        actions.push({ name: 'revise', label: '::Revise', icon: 'code-branch', color: 'primary' });
        actions.push({ name: 'lost', label: '::MarkLost', icon: 'thumbs-down', color: 'warning' });
      }
      actions.push({ name: 'cancel', label: '::Cancel', icon: 'ban', color: 'danger' });
    }
    if (this.quotation.status === 'Cancelled' || this.quotation.status === 'Rejected') {
      actions.push({ name: 'amend', label: '::Amend', icon: 'file-circle-plus', color: 'success' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: (result) => {
      this.quotation = result;
      // Load company data for print layout
      if ((result as any).companyId) {
        this.companyService.get((result as any).companyId).subscribe({
          next: (company) => this.companyData.set(company),
          error: () => {} // Non-critical — print still works without company header
        });
      }
    }, error: () => {} });
    this.competitorService.getList({ maxResultCount: 1000, skipCount: 0 }).subscribe({
      next: (res: any) => this.competitors.set(res?.items ?? []),
      error: () => {}
    });
    this.lostReasonService.getAllList().subscribe({
      next: (list) => this.lostReasons.set(list ?? []),
      error: () => {}
    });
  }

  competitorName(competitorId: string | undefined): string {
    return this.competitors().find(c => c.id === competitorId)?.name ?? competitorId ?? '';
  }

  lostReasonLabel(detail: { reason?: string | null; quotationLostReasonId?: string }): string {
    if (detail.reason) return detail.reason;
    return this.lostReasons().find(r => r.id === detail.quotationLostReasonId)?.reason ?? detail.quotationLostReasonId ?? '';
  }

  addLostReason(): void {
    const quotationId = this.quotation?.id;
    if (!quotationId || !this.selectedLostReasonId) return;
    this.service.createLostReason({
      quotationId,
      quotationLostReasonId: this.selectedLostReasonId,
    }).subscribe({
      next: () => {
        this.selectedLostReasonId = '';
        this.toaster.success('::SuccessfullyUpdated');
        this.reloadAfterAction();
      },
      error: () => {}
    });
  }

  removeLostReason(detailId: string | undefined): void {
    if (!detailId) return;
    this.service.deleteLostReason(detailId).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyDeleted');
        this.reloadAfterAction();
      },
      error: () => {}
    });
  }

  addCompetitor(): void {
    const quotationId = this.quotation?.id;
    if (!quotationId || !this.selectedCompetitorId) return;

    this.service.createCompetitor({
      parentType: 'Quotation',
      parentId: quotationId,
      competitorId: this.selectedCompetitorId,
    }).subscribe({
      next: () => {
        this.selectedCompetitorId = '';
        this.reloadAfterAction();
      },
      error: () => {}
    });
  }

  removeCompetitor(competitorDetailId: string | undefined): void {
    if (!competitorDetailId) return;
    this.service.deleteCompetitor(competitorDetailId).subscribe({
      next: () => this.reloadAfterAction(),
      error: () => {}
    });
  }

  get isExpired(): boolean {
    if (!this.quotation) return false;
    const validUntil = (this.quotation as any).validUntil;
    if (!validUntil) return false;
    return new Date(validUntil) < new Date() && this.quotation.status === 'Submitted';
  }

  /** Ids the user ticked for conversion; null until they touch a checkbox (then non-alternative rows are the default). */
  private orderSelection: Set<string> | null = null;

  get hasAlternatives(): boolean {
    return (this.quotation?.items ?? []).some((r: any) => r.isAlternative);
  }

  isSelectedForOrder(row: any): boolean {
    return this.orderSelection ? this.orderSelection.has(row.id) : !row.isAlternative;
  }

  toggleOrderSelection(row: any, checked: boolean): void {
    if (!this.orderSelection) {
      this.orderSelection = new Set((this.quotation?.items ?? []).filter((r: any) => !r.isAlternative).map((r: any) => r.id));
    }
    if (checked) this.orderSelection.add(row.id); else this.orderSelection.delete(row.id);
  }

  get daysUntilExpiry(): number | null {
    if (!this.quotation) return null;
    const validUntil = (this.quotation as any).validUntil;
    if (!validUntil) return null;
    const diff = new Date(validUntil).getTime() - new Date().getTime();
    return Math.ceil(diff / (1000 * 60 * 60 * 24));
  }

  private resolveSelectedOrderItemIds(): string[] | undefined | null {
    if (!this.hasAlternatives) {
      return undefined;
    }
    const ids = (this.quotation!.items ?? []).filter(r => this.isSelectedForOrder(r)).map(r => r.id!);
    if (ids.length === 0) {
      this.toaster.error('Select at least one item to order.');
      return null;
    }
    return ids;
  }

  onWorkflowAction(action: string): void {
    const id = this.quotation!.id!;
    switch (action) {
      case 'submit':
        this.store.submitQuotation(id);
        this.reloadAfterAction();
        break;
      case 'convert': {
        const selectedItemIds = this.resolveSelectedOrderItemIds();
        if (selectedItemIds === null) break;
        this.conversionService.convertQuotationToSalesOrder(id, selectedItemIds).subscribe((salesOrder) => {
          this.router.navigate(['/sales/orders', salesOrder.id]);
        });
        break;
      }
      case 'convertToInvoice': {
        const selectedItemIds = this.resolveSelectedOrderItemIds();
        if (selectedItemIds === null) break;
        this.conversionService.convertQuotationToSalesInvoice(id, selectedItemIds).subscribe({
          next: (invoice) => this.router.navigate(['/sales/invoices', invoice.id]),
          error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
        });
        break;
      }
      case 'revise':
        this.service.createRevision(id).subscribe({
          next: (revised) => this.router.navigate(['/sales/quotations', revised.id]),
          error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
        });
        break;
      case 'lost':
        this.service.markLost(id).subscribe({ next: () => this.reloadAfterAction(), error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed') });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelQuotation(id);
            this.reloadAfterAction();
          }
        });
        break;
      case 'amend':
        this.service.amend(id).subscribe({
          next: (amended) => this.router.navigate(['/sales/quotations', amended.id]),
        });
        break;
      case 'sendEmail':
        this.openSendEmailDialog();
        break;
    }
  }

  private reloadAfterAction(): void {
    this.service.get(this.quotation!.id!).subscribe({
      next: (r) => { this.quotation = r; },
      error: () => {}
    });
  }

  print(): void {
    window.print();
  }

  downloadPdf(): void {
    if (!this.quotation?.id) return;
    this.documentPrintService.getQuotationPrint(this.quotation.id).subscribe({
      next: (result) => {
        if (!downloadPdfFromResult(result, 'quotation.pdf')) {
          this.toaster.error('::OperationFailed');
        }
      },
      error: () => this.toaster.error('::OperationFailed'),
    });
  }

  openSendEmailDialog(): void {
    this.emailRecipient = (this.quotation as any)?.customerEmail || '';
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
    this.emailService.sendQuotationEmail({
      quotationId: this.quotation!.id,
      recipientEmail: this.emailRecipient,
      ccEmails: this.emailCc ? this.emailCc.split(',').map((e: string) => e.trim()) : null,
      attachPdf: this.emailAttachPdf,
    } as any).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySent');
        this.showEmailDialog = false;
        this.emailSending = false;
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        this.emailSending = false;
      },
    });
  }

  cancelEmailDialog(): void {
    this.showEmailDialog = false;
  }
}
