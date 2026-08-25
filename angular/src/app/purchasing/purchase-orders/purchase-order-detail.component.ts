import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { DraftLinkGuardComponent } from '../../shared/components/draft-link-guard/draft-link-guard.component';
import { PurchaseOrderPrintLayoutComponent } from '../../shared/components/po-print-layout/po-print-layout.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { PartyPerformanceService } from '../../proxy/core/party-performance.service';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { PurchaseOrderStore } from '../store/purchase-order.store';
import type { PurchaseOrderDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-order-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DocumentWorkflowComponent, LoadingOverlayComponent, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, RouterLink, DraftLinkGuardComponent, PurchaseOrderPrintLayoutComponent, StatusBadgeComponent, DocumentConnectionsComponent],
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
  private l = inject(LocalizationService);
  private documentEmailService = inject(DocumentEmailService);
  private supplierProxyService = inject(SupplierService);
  private partyPerformanceService = inject(PartyPerformanceService);

  order: PurchaseOrderDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];
  orderReceipts = signal<any[]>([]);
  orderPayments = signal<any[]>([]);
  supplierContact = signal<{ phone?: string; email?: string; contactPerson?: string } | null>(null);

  // Supplier Confirmation
  showConfirmationForm = signal(false);
  confirmationNumber = '';
  promisedDate = '';
  isRecordingConfirmation = signal(false);

  supplierPerformance = signal<any>(null);

  // Company info for print layout
  companyName = '';
  companyTin = '';
  companySst = '';

  // Send Email dialog state
  showEmailDialog = false;
  emailRecipient = '';
  emailCc = '';
  emailAttachPdf = true;
  emailSending = false;

  // Draft Link Guard state
  showDraftGuard = signal(false);
  draftGuardTarget = signal<'PurchaseReceipt' | 'PurchaseInvoice' | null>(null);
  private pendingConversionAction: (() => void) | null = null;

  // Update Items inline editing state
  isEditingItems = signal(false);
  editableItems = signal<Array<{ itemId: string; quantity: number; unitPrice: number; description: string; receivedQty: number }>>([]);
  removedItemIds = signal<string[]>([]);
  isSavingItems = signal(false);

  get workflowActions(): WorkflowAction[] {
    if (!this.order) return [];
    const actions: WorkflowAction[] = [];
    const s = this.order.status;

    if (s === 'Draft') {
      actions.push({ name: 'submit', label: this.l.instant('::Submit'), icon: 'paper-plane', color: 'primary' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToDeliver') {
      actions.push({ name: 'receipt', label: this.l.instant('::MakeReceipt'), icon: 'box-open', color: 'info' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToBill') {
      actions.push({ name: 'invoice', label: this.l.instant('::CreateInvoice'), icon: 'file-invoice', color: 'info' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToDeliver' || s === 'ToBill') {
      actions.push({ name: 'payment', label: this.l.instant('::MakePayment'), icon: 'money-bill', color: 'info' });
    }
    if (s !== 'Draft' && s !== 'Cancelled') {
      actions.push({ name: 'sendEmail', label: this.l.instant('::SendEmail'), icon: 'envelope', color: 'secondary' });
    }
    if (s !== 'Draft' && s !== 'Cancelled' && s !== 'Completed' && s !== 'Closed') {
      actions.push({ name: 'close', label: this.l.instant('::Close'), icon: 'lock', color: 'warning' });
      actions.push({ name: 'cancel', label: this.l.instant('::Cancel'), icon: 'ban', color: 'danger' });
    }
    if (s === 'Closed') {
      actions.push({ name: 'reopen', label: this.l.instant('::Reopen'), icon: 'lock-open', color: 'primary' });
    }
    if (s === 'Cancelled') {
      actions.push({ name: 'amend', label: this.l.instant('::Amend'), icon: 'file-circle-plus', color: 'success' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.order = result;
      if (result.status !== 'Draft') {
        this.loadReceiptsAndPayments(id);
      }
      // Load supplier performance metrics + contact info
      if ((result as any).supplierId) {
        this.partyPerformanceService.getSupplierPerformance((result as any).supplierId).subscribe({
          next: (perf: any) => this.supplierPerformance.set(perf),
          error: () => {}
        });
        this.supplierProxyService.get((result as any).supplierId).subscribe({
          next: (s: any) => this.supplierContact.set({ phone: s?.phone, email: s?.email, contactPerson: s?.contactPerson }),
          error: () => {}
        });
      }
    });
  }

  printOrder(): void {
    window.print();
  }

  private loadReceiptsAndPayments(orderId: string): void {
    this.service.getOrderReceipts(orderId).subscribe({
      next: (receipts) => this.orderReceipts.set(receipts ?? []),
      error: () => {}
    });
    this.service.getOrderPayments(orderId).subscribe({
      next: (payments) => this.orderPayments.set(payments ?? []),
      error: () => {}
    });
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
          queryParams: {
            partyType: 'Supplier',
            partyId: this.order!.supplierId,
            againstOrderType: 'PurchaseOrder',
            againstOrderId: id,
            companyId: this.order!.companyId,
          }
        });
        break;
      case 'close':
        this.service.close(id).subscribe({ next: () => this.reloadAfterAction(), error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed') });
        break;
      case 'reopen':
        this.service.reopen(id).subscribe({ next: () => this.reloadAfterAction(), error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed') });
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
      case 'sendEmail':
        this.openSendEmailDialog();
        break;
    }
  }

  private reloadAfterAction(): void {
    this.service.get(this.order!.id!).subscribe({
      next: (r) => { this.order = r; },
      error: () => {}
    });
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
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(this.order!.id!).subscribe({
        next: () => this.router.navigate(['/purchasing/orders']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    });
  }

  /** Records supplier confirmation/acknowledgment of this purchase order. */
  recordSupplierConfirmation(): void {
    if (!this.order?.id) return;
    this.isRecordingConfirmation.set(true);
    this.service.recordSupplierConfirmation(this.order.id, {
      confirmationNumber: this.confirmationNumber || undefined,
      confirmationDate: new Date().toISOString(),
      promisedDeliveryDate: this.promisedDate || undefined,
    }).subscribe({
      next: (updated) => {
        this.order = updated;
        this.showConfirmationForm.set(false);
        this.isRecordingConfirmation.set(false);
        this.toaster.success(this.l.instant('::SupplierConfirmationRecorded'));
      },
      error: (err: any) => {
        this.isRecordingConfirmation.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }

  /** Per-item receipt progress percentage (capped at 100%) */
  getItemReceiptPct(row: any): number {
    if (!row.quantity || row.quantity <= 0) return 0;
    return Math.min(100, ((row.receivedQty ?? 0) / row.quantity) * 100);
  }

  /** Per-item billing progress percentage (capped at 100%) */
  getItemBilledPct(row: any): number {
    if (!row.quantity || row.quantity <= 0) return 0;
    return Math.min(100, ((row.billedQty ?? 0) / row.quantity) * 100);
  }

  /** Whether the PO expected delivery date is overdue */
  isOverdue(): boolean {
    if (!this.order?.expectedDeliveryDate) return false;
    if (this.order.status === 'Completed' || this.order.status === 'Closed' || this.order.status === 'Cancelled' || this.order.status === 'Draft') return false;
    const expectedDate = new Date(this.order.expectedDeliveryDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return expectedDate < today;
  }

  /** Get the effective expected delivery date for an item (item-level or parent fallback) */
  getEffectiveExpectedDate(row: any): string | null {
    const d = row.expectedDeliveryDate ?? this.order?.expectedDeliveryDate;
    if (!d) return null;
    return new Date(d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  /** Whether a specific item is overdue (past expected date + has pending receipt qty) */
  isItemOverdue(row: any): boolean {
    const d = row.expectedDeliveryDate ?? this.order?.expectedDeliveryDate;
    if (!d) return false;
    const pendingQty = (row.quantity ?? 0) - (row.receivedQty ?? 0);
    if (pendingQty <= 0) return false;
    const expected = new Date(d);
    expected.setHours(0, 0, 0, 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return expected < today;
  }

  openSendEmailDialog(): void {
    this.emailRecipient = (this.order as any)?.supplierEmail || '';
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
    this.documentEmailService.sendPurchaseOrderEmail({
      documentId: this.order!.id,
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

  /** Number of days past the expected delivery date */
  overdueDays(): number {
    if (!this.order?.expectedDeliveryDate) return 0;
    const expectedDate = new Date(this.order.expectedDeliveryDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const diff = today.getTime() - expectedDate.getTime();
    return Math.max(0, Math.floor(diff / (1000 * 60 * 60 * 24)));
  }

  /** Whether Update Items button should be visible (active submitted orders only) */
  canUpdateItems(): boolean {
    if (!this.order) return false;
    const s = this.order.status;
    return s === 'ToDeliverAndBill' || s === 'ToDeliver' || s === 'ToBill';
  }

  /** Enter inline item editing mode */
  startEditingItems(): void {
    if (!this.order?.items) return;
    this.editableItems.set(this.order.items.map(item => ({
      itemId: item.id,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      description: item.description,
      receivedQty: item.receivedQty ?? 0,
    })));
    this.removedItemIds.set([]);
    this.isEditingItems.set(true);
  }

  /** Cancel inline editing */
  cancelEditingItems(): void {
    this.isEditingItems.set(false);
    this.editableItems.set([]);
    this.removedItemIds.set([]);
  }

  /** Mark a row for removal (blocked server-side if already received/billed). */
  removeEditableItem(itemId: string): void {
    this.editableItems.set(this.editableItems().filter(i => i.itemId !== itemId));
    this.removedItemIds.set([...this.removedItemIds(), itemId]);
  }

  /** Save updated items to backend */
  saveItemUpdates(): void {
    if (!this.order?.id) return;
    this.isSavingItems.set(true);
    const payload = {
      items: this.editableItems().map(item => ({
        itemId: item.itemId,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
      })),
      removedItemIds: this.removedItemIds(),
    };
    this.service.updateItems(this.order.id, payload).subscribe({
      next: (result) => {
        this.toaster.success(this.l.instant('::ItemsUpdatedSuccessfully'));
        this.isEditingItems.set(false);
        this.isSavingItems.set(false);
        this.removedItemIds.set([]);
        this.reloadAfterAction();
      },
      error: (err: any) => {
        this.isSavingItems.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }
}
