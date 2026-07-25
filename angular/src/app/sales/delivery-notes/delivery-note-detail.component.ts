import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router } from '@angular/router';
import { CompanyService } from '../../proxy/core/company.service';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { DraftLinkGuardComponent } from '../../shared/components/draft-link-guard/draft-link-guard.component';
import { DeliveryNotePrintLayoutComponent } from '../../shared/components/dn-print-layout/dn-print-layout.component';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { DeliveryNoteStore } from '../store/delivery-note.store';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import type { DeliveryNoteDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-delivery-note-detail',
  standalone: true,
  imports: [
    CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, DraftLinkGuardComponent, DeliveryNotePrintLayoutComponent, VoucherLedgerComponent],
  templateUrl: './delivery-note-detail.component.html',
  styleUrls: ['./delivery-note-detail.component.scss'],
})
export class DeliveryNoteDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(DeliveryNoteService);
  private conversionService = inject(DocumentConversionService);
  private store = inject(DeliveryNoteStore);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private router = inject(Router);
  private companyService = inject(CompanyService);

  deliveryNote: DeliveryNoteDto | null = null;
  itemColumns = ['description', 'quantity', 'uom'];

  // Print layout data
  companyName = '';
  companyTin = '';
  companySst = '';
  companyAddress = '';
  warehouseName = '';

  // Draft Link Guard state
  showDraftGuard = signal(false);
  private pendingConversionAction: (() => void) | null = null;

  get workflowActions(): WorkflowAction[] {
    if (!this.deliveryNote) return [];
    const actions: WorkflowAction[] = [];
    switch (this.deliveryNote.status) {
      case 'Draft':
        actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
        break;
      case 'Submitted':
        actions.push({ name: 'invoice', label: 'Make Invoice', icon: 'receipt', color: 'primary' });
        actions.push({ name: 'return', label: 'Create Return', icon: 'rotate-left', color: 'warning' });
        actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
        break;
      case 'Cancelled':
        actions.push({ name: 'amend', label: 'Amend', icon: 'file-circle-plus', color: 'success' });
        break;
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.deliveryNote = result;
      this.loadCompanyData(result.companyId);
    });
  }

  printDeliveryNote(): void {
    window.print();
  }

  private loadCompanyData(companyId: string | undefined): void {
    if (!companyId) return;
    this.companyService.get(companyId).subscribe({
      next: (company) => {
        this.companyName = company.name || '';
        this.companyTin = company.taxId || '';
        this.companySst = company.sstRegistrationNumber || '';
        this.companyAddress = company.address || '';
      },
      error: () => {},
    });
  }

  onWorkflowAction(action: string): void {
    const id = this.deliveryNote!.id!;
    switch (action) {
      case 'submit':
        this.store.submitNote(id);
        this.reloadAfterAction();
        break;
      case 'invoice':
        this.pendingConversionAction = () => {
          this.conversionService.convertDeliveryNoteToSalesInvoice(id).subscribe({
            next: (inv) => this.router.navigate(['/sales/invoices', inv.id]),
            error: () => this.toaster.error('::ConversionFailed'),
          });
        };
        this.showDraftGuard.set(true);
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelNote(id);
            this.reloadAfterAction();
          }
        });
        break;
      case 'return':
        this.router.navigate(['/sales/delivery-notes/new'], {
          queryParams: { returnAgainst: id }
        });
        break;
      case 'amend':
        this.service.amend(id).subscribe({
          next: (amended) => this.router.navigate(['/sales/delivery-notes', amended.id]),
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.deliveryNote!.id!).subscribe((result) => {
        this.deliveryNote = result;
      });
    }, 500);
  }

  onDraftGuardProceed(): void {
    this.showDraftGuard.set(false);
    if (this.pendingConversionAction) {
      this.pendingConversionAction();
      this.pendingConversionAction = null;
    }
  }

  onDraftGuardCancelled(): void {
    this.showDraftGuard.set(false);
    this.pendingConversionAction = null;
  }
}
