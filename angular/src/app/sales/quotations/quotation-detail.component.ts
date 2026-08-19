import { Component, inject, OnInit, signal } from '@angular/core';
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
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { QuotationStore } from '../store/quotation.store';
import type { QuotationDto } from '../../proxy/sales/models';
import { CompetitorService } from '../../proxy/crm/competitor.service';
import type { CompetitorDto } from '../../proxy/crm/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { QuotationPrintLayoutComponent } from '../../shared/components/quotation-print-layout/quotation-print-layout.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';

@Component({
  selector: 'app-quotation-detail',
  standalone: true,
  imports: [
    BreadcrumbComponent, CommonModule, FormsModule, RouterModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe, ActivityLogComponent, QuotationPrintLayoutComponent, DocumentConnectionsComponent, CompanyCurrencyPipe],
  templateUrl: './quotation-detail.component.html',
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

  quotation: QuotationDto | null = null;
  companyData = signal<any>(null);
  competitors = signal<CompetitorDto[]>([]);
  selectedCompetitorId = '';
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
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (this.quotation.status === 'Submitted') {
      actions.push({ name: 'convert', label: 'Convert to SO', icon: 'arrow-right-arrow-left', color: 'success' });
      actions.push({ name: 'sendEmail', label: this.l.instant('::SendEmail'), icon: 'envelope', color: 'secondary' });
      actions.push({ name: 'lost', label: 'Mark Lost', icon: 'thumbs-down', color: 'warning' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    if (this.quotation.status === 'Cancelled' || this.quotation.status === 'Rejected') {
      actions.push({ name: 'amend', label: 'Amend', icon: 'file-circle-plus', color: 'success' });
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
  }

  competitorName(competitorId: string | undefined): string {
    return this.competitors().find(c => c.id === competitorId)?.name ?? competitorId ?? '';
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

  get daysUntilExpiry(): number | null {
    if (!this.quotation) return null;
    const validUntil = (this.quotation as any).validUntil;
    if (!validUntil) return null;
    const diff = new Date(validUntil).getTime() - new Date().getTime();
    return Math.ceil(diff / (1000 * 60 * 60 * 24));
  }

  onWorkflowAction(action: string): void {
    const id = this.quotation!.id!;
    switch (action) {
      case 'submit':
        this.store.submitQuotation(id);
        this.reloadAfterAction();
        break;
      case 'convert':
        this.conversionService.convertQuotationToSalesOrder(id).subscribe((salesOrder) => {
          this.router.navigate(['/sales/orders', salesOrder.id]);
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
