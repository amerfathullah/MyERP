import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../proxy/core/company.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { DraftLinkGuardComponent } from '../../shared/components/draft-link-guard/draft-link-guard.component';
import { SalesOrderAmendmentService } from '../../proxy/sales/sales-order-amendment.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { SalesOrderStore } from '../store/sales-order.store';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { SalesOrderPrintLayoutComponent } from '../../shared/components/so-print-layout/so-print-layout.component';
import type { SalesOrderDto, DeliveryScheduleEntryDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-sales-order-detail',
  standalone: true,
  imports: [
    CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, RouterLink, DraftLinkGuardComponent, SalesOrderPrintLayoutComponent],
  templateUrl: './sales-order-detail.component.html',
  styleUrls: ['./sales-order-detail.component.scss'],
})
export class SalesOrderDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(SalesOrderService);
  private conversionService = inject(DocumentConversionService);
  private store = inject(SalesOrderStore);
  private confirmation = inject(ConfirmationService);
  private amendmentService = inject(SalesOrderAmendmentService);
  private toaster = inject(ToasterService);
  private companyService = inject(CompanyService);
  private salesOrderService = inject(SalesOrderService);

  order: SalesOrderDto | null = null;
  deliverySchedule = signal<any[]>([]);
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  // Print layout data
  companyName = '';
  companyTin = '';
  companySst = '';
  companyAddress = '';

  // Draft Link Guard state
  showDraftGuard = signal(false);
  draftGuardTarget = signal<'DeliveryNote' | 'SalesInvoice' | null>(null);
  private pendingConversionAction: (() => void) | null = null;

  get workflowActions(): WorkflowAction[] {
    if (!this.order) return [];
    const actions: WorkflowAction[] = [];
    const s = this.order.status;

    if (s === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToDeliver') {
      actions.push({ name: 'delivery', label: 'Create Delivery Note', icon: 'local_shipping', color: 'primary' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToBill') {
      actions.push({ name: 'invoice', label: 'Create Invoice', icon: 'receipt', color: 'accent' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToDeliver' || s === 'ToBill') {
      actions.push({ name: 'payment', label: 'Make Payment', icon: 'payment', color: 'accent' });
      actions.push({ name: 'work_order', label: 'Make Work Order', icon: 'factory', color: 'accent' });
    }
    if (s !== 'Draft' && s !== 'Cancelled' && s !== 'Completed' && s !== 'Closed') {
      actions.push({ name: 'close', label: 'Close', icon: 'lock', color: 'warn' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
    }
    if (s === 'Closed') {
      actions.push({ name: 'reopen', label: 'Reopen', icon: 'lock_open', color: 'primary' });
    }
    if (s === 'Cancelled') {
      actions.push({ name: 'amend', label: 'Amend', icon: 'file-circle-plus', color: 'success' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.order = result;
      this.loadCompanyData(result.companyId);
      // Load delivery schedule entries
      this.salesOrderService.getDeliverySchedule(id).subscribe({
        next: (entries) => this.deliverySchedule.set(entries ?? []),
        error: () => {} // graceful — schedule is optional
      });
    });
  }

  printOrder(): void {
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
    const id = this.order!.id!;
    switch (action) {
      case 'submit':
        this.store.submitOrder(id);
        this.reloadAfterAction();
        break;
      case 'delivery':
        this.initiateConversion('DeliveryNote', () => {
          this.conversionService.convertSalesOrderToDeliveryNote(id).subscribe({
            next: (dn) => this.router.navigate(['/sales/delivery-notes', dn.id]),
            error: () => this.toaster.error('::ConversionFailed'),
          });
        });
        break;
      case 'invoice':
        this.initiateConversion('SalesInvoice', () => {
          this.conversionService.convertSalesOrderToSalesInvoice(id).subscribe({
            next: (inv) => this.router.navigate(['/sales/invoices', inv.id]),
            error: () => this.toaster.error('::ConversionFailed'),
          });
        });
        break;
      case 'payment':
        this.router.navigate(['/accounting/payments/new'], {
          queryParams: { partyType: 'Customer', againstOrderType: 'SalesOrder', againstOrderId: id }
        });
        break;
      case 'work_order':
        this.router.navigate(['/manufacturing/work-orders/new'], {
          queryParams: { salesOrderId: id, companyId: this.order!.companyId }
        });
        break;
      case 'close':
        this.service.close(id).subscribe(() => this.reloadAfterAction());
        break;
      case 'reopen':
        this.service.reopen(id).subscribe(() => this.reloadAfterAction());
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelOrder(id);
            this.reloadAfterAction();
          }
        });
        break;
      case 'amend':
        this.amendmentService.amend(id).subscribe({
          next: (amendedId) => this.router.navigate(['/sales/orders', amendedId]),
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.order!.id!).subscribe((r) => { this.order = r; });
    }, 500);
  }

  /** Triggers the draft link guard check before executing a conversion action. */
  private initiateConversion(targetDocType: 'DeliveryNote' | 'SalesInvoice', action: () => void): void {
    this.pendingConversionAction = action;
    this.draftGuardTarget.set(targetDocType);
    this.showDraftGuard.set(true);
  }

  /** Called when DraftLinkGuard confirms safe to proceed (no drafts or user clicked "Create Anyway"). */
  onDraftGuardProceed(): void {
    this.showDraftGuard.set(false);
    this.draftGuardTarget.set(null);
    if (this.pendingConversionAction) {
      this.pendingConversionAction();
      this.pendingConversionAction = null;
    }
  }

  /** Called when user cancels the conversion from the draft guard warning. */
  onDraftGuardCancelled(): void {
    this.showDraftGuard.set(false);
    this.draftGuardTarget.set(null);
    this.pendingConversionAction = null;
  }

  deleteOrder(): void {
    if (!confirm('Are you sure you want to delete this draft order?')) return;
    this.service.delete(this.order!.id!).subscribe({
      next: () => this.router.navigate(['/sales/orders']),
      error: () => {},
    });
  }
}
