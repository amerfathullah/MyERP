import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { DraftLinkGuardComponent } from '../../shared/components/draft-link-guard/draft-link-guard.component';
import { PurchaseOrderPrintLayoutComponent } from '../../shared/components/po-print-layout/po-print-layout.component';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { PurchaseOrderStore } from '../store/purchase-order.store';
import type { PurchaseOrderDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-order-detail',
  standalone: true,
  imports: [
    CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, RouterLink, DraftLinkGuardComponent, PurchaseOrderPrintLayoutComponent],
  templateUrl: './purchase-order-detail.component.html',
  styleUrls: ['./purchase-order-detail.component.scss'],
})
export class PurchaseOrderDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(PurchaseOrderService);
  private conversionService = inject(PurchaseConversionService);
  private store = inject(PurchaseOrderStore);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  order: PurchaseOrderDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  // Company info for print layout
  companyName = '';
  companyTin = '';
  companySst = '';

  // Draft Link Guard state
  showDraftGuard = signal(false);
  draftGuardTarget = signal<'PurchaseReceipt' | 'PurchaseInvoice' | null>(null);
  private pendingConversionAction: (() => void) | null = null;

  get workflowActions(): WorkflowAction[] {
    if (!this.order) return [];
    const actions: WorkflowAction[] = [];
    const s = this.order.status;

    if (s === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToDeliver') {
      actions.push({ name: 'receipt', label: 'Make Receipt', icon: 'inventory_2', color: 'primary' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToBill') {
      actions.push({ name: 'invoice', label: 'Make Invoice', icon: 'receipt', color: 'accent' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToDeliver' || s === 'ToBill') {
      actions.push({ name: 'payment', label: 'Make Payment', icon: 'payment', color: 'accent' });
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
    });
  }

  printOrder(): void {
    window.print();
  }

  onWorkflowAction(action: string): void {
    const id = this.order!.id!;
    switch (action) {
      case 'submit':
        this.store.submitOrder(id);
        this.reloadAfterAction();
        break;
      case 'receipt':
        this.initiateConversion('PurchaseReceipt', () => {
          this.conversionService.convertPurchaseOrderToReceipt(id).subscribe({
            next: (receipt) => this.router.navigate(['/purchasing/receipts', receipt.id]),
            error: () => this.toaster.error('::ConversionFailed'),
          });
        });
        break;
      case 'invoice':
        this.initiateConversion('PurchaseInvoice', () => {
          this.conversionService.convertPurchaseOrderToInvoice(id).subscribe({
            next: (invoice) => this.router.navigate(['/purchasing/invoices', invoice.id]),
            error: () => this.toaster.error('::ConversionFailed'),
          });
        });
        break;
      case 'payment':
        this.router.navigate(['/accounting/payments/new'], {
          queryParams: { partyType: 'Supplier', againstOrderType: 'PurchaseOrder', againstOrderId: id }
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
        this.service.amend(id).subscribe({
          next: (amended) => this.router.navigate(['/purchasing/orders', amended.id]),
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.order!.id!).subscribe((r) => { this.order = r; });
    }, 500);
  }

  private initiateConversion(targetDocType: 'PurchaseReceipt' | 'PurchaseInvoice', action: () => void): void {
    this.pendingConversionAction = action;
    this.draftGuardTarget.set(targetDocType);
    this.showDraftGuard.set(true);
  }

  onDraftGuardProceed(): void {
    this.showDraftGuard.set(false);
    this.draftGuardTarget.set(null);
    if (this.pendingConversionAction) {
      this.pendingConversionAction();
      this.pendingConversionAction = null;
    }
  }

  onDraftGuardCancelled(): void {
    this.showDraftGuard.set(false);
    this.draftGuardTarget.set(null);
    this.pendingConversionAction = null;
  }

  deleteOrder(): void {
    if (!confirm('Are you sure you want to delete this draft order?')) return;
    this.service.delete(this.order!.id!).subscribe({
      next: () => this.router.navigate(['/purchasing/orders']),
      error: () => {},
    });
  }
}
