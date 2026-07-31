import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
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
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { SalesOrderPrintLayoutComponent } from '../../shared/components/so-print-layout/so-print-layout.component';
import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';
import type { SalesOrderDto, DeliveryScheduleEntryDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-sales-order-detail',
  standalone: true,
  imports: [
    CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, RouterLink, DraftLinkGuardComponent, SalesOrderPrintLayoutComponent, FormsModule, DocumentConnectionsComponent, CompanyCurrencyPipe],
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
  private http = inject(HttpClient);
  private amendmentService = inject(SalesOrderAmendmentService);
  private toaster = inject(ToasterService);
  private companyService = inject(CompanyService);
  private salesOrderService = inject(SalesOrderService);
  private l = inject(LocalizationService);

  order: SalesOrderDto | null = null;
  deliverySchedule = signal<any[]>([]);
  orderPayments = signal<any[]>([]);
  itemStock = signal<Record<string, number>>({});
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

  // Partial Delivery Selection state
  showDeliverySelection = signal(false);
  deliverySelectionItems = signal<{ itemId: string; description: string; pendingQty: number; deliverQty: number; selected: boolean }[]>([]);
  isCreatingDN = signal(false);

  // Delivery Schedule Generator state
  showScheduleGenerator = signal(false);
  scheduleFrequency = 'Monthly';
  scheduleItemId = '';
  isGeneratingSchedule = signal(false);

  showEmailDialog = false;
  emailRecipient = '';
  emailCc = '';
  emailAttachPdf = true;
  emailSending = false;

  isActiveOrder(): boolean {
    const s = this.order?.status;
    return s === 'ToDeliverAndBill' || s === 'ToDeliver' || s === 'ToBill';
  }

  isDeliveryOverdue(): boolean {
    if (!this.order?.deliveryDate || !this.isActiveOrder()) return false;
    if (this.order.status === 'ToBill') return false; // already delivered
    const deliveryDate = new Date(this.order.deliveryDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return deliveryDate < today;
  }

  overdueDays(): number {
    if (!this.order?.deliveryDate) return 0;
    const deliveryDate = new Date(this.order.deliveryDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return Math.max(0, Math.floor((today.getTime() - deliveryDate.getTime()) / (1000 * 60 * 60 * 24)));
  }

  /** Per-item delivery progress percentage (capped at 100%) */
  getItemDeliveryPct(row: any): number {
    if (!row.quantity || row.quantity <= 0) return 0;
    return Math.min(100, ((row.deliveredQty ?? 0) / row.quantity) * 100);
  }

  /** Per-item billing progress percentage (capped at 100%) */
  getItemBilledPct(row: any): number {
    if (!row.quantity || row.quantity <= 0) return 0;
    return Math.min(100, ((row.billedQty ?? 0) / row.quantity) * 100);
  }

  /** Per-item delivery date overdue check: past due + not fully delivered */
  isItemOverdue(row: any): boolean {
    if (!row.deliveryDate) return false;
    const dueDate = new Date(row.deliveryDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const deliveredQty = row.deliveredQty ?? 0;
    return dueDate < today && deliveredQty < row.quantity;
  }

  getTimelineSteps(): { label: string; icon: string; completed: boolean; active: boolean; date?: string }[] {
    const o = this.order;
    if (!o) return [];
    const perDel = o.perDelivered ?? 0;
    const perBill = o.perBilled ?? 0;
    return [
      { label: this.l.instant('::Ordered'), icon: 'fa fa-clipboard-check', completed: true, active: false, date: o.orderDate },
      { label: this.l.instant('::Delivered'), icon: 'fa fa-truck', completed: perDel >= 100, active: perDel > 0 && perDel < 100, date: o.firstDeliveryDate ?? undefined },
      { label: this.l.instant('::Billed'), icon: 'fa fa-file-invoice', completed: perBill >= 100, active: perBill > 0 && perBill < 100, date: o.firstBilledDate ?? undefined },
      { label: this.l.instant('::Paid'), icon: 'fa fa-circle-check', completed: o.status === 'Completed', active: (o as any).advancePaid > 0 && o.status !== 'Completed', date: o.firstPaymentDate ?? undefined },
    ];
  }

  generateDeliverySchedule(): void {
    if (!this.order?.id || !this.scheduleItemId) return;
    this.isGeneratingSchedule.set(true);
    this.salesOrderService.generateDeliverySchedule(this.order.id, this.scheduleItemId, this.scheduleFrequency).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyGenerated');
        this.isGeneratingSchedule.set(false);
        this.showScheduleGenerator.set(false);
        // Reload schedule entries
        this.salesOrderService.getDeliverySchedule(this.order!.id!).subscribe({
          next: (entries) => this.deliverySchedule.set(entries ?? []),
          error: () => {}
        });
      },
      error: (err: any) => {
        this.isGeneratingSchedule.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      }
    });
  }

  get workflowActions(): WorkflowAction[] {
    if (!this.order) return [];
    const actions: WorkflowAction[] = [];
    const s = this.order.status;

    if (s === 'Draft') {
      actions.push({ name: 'submit', label: this.l.instant('::Submit'), icon: 'paper-plane', color: 'primary' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToDeliver') {
      actions.push({ name: 'delivery', label: this.l.instant('::CreateDeliveryNote'), icon: 'truck', color: 'info' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToBill') {
      actions.push({ name: 'invoice', label: this.l.instant('::CreateInvoice'), icon: 'file-invoice', color: 'info' });
    }
    if (s === 'ToDeliverAndBill' || s === 'ToDeliver' || s === 'ToBill') {
      actions.push({ name: 'payment', label: this.l.instant('::MakePayment'), icon: 'money-bill', color: 'info' });
      actions.push({ name: 'pick_list', label: this.l.instant('::CreatePickList'), icon: 'clipboard-list', color: 'info' });
      actions.push({ name: 'work_order', label: this.l.instant('::MakeWorkOrder'), icon: 'industry', color: 'info' });
      actions.push({ name: 'production_plan', label: this.l.instant('::CreateProductionPlan'), icon: 'chart-gantt', color: 'info' });
      actions.push({ name: 'material_request', label: this.l.instant('::MaterialRequest'), icon: 'box-open', color: 'info' });
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
      this.loadCompanyData(result.companyId);
      // Load delivery schedule entries
      this.salesOrderService.getDeliverySchedule(id).subscribe({
        next: (entries) => this.deliverySchedule.set(entries ?? []),
        error: () => {}
      });
      // Load linked payment entries
      this.salesOrderService.getOrderPayments(id).subscribe({
        next: (payments) => this.orderPayments.set(payments ?? []),
        error: () => {}
      });
      // Load per-item stock availability for active orders
      if (result.status !== 'Draft' && result.status !== 'Cancelled' && result.items?.length) {
        this.loadItemStockAvailability(result.items);
      }
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

  private loadItemStockAvailability(items: any[]): void {
    const itemIds = [...new Set(items.map((i: any) => i.itemId).filter(Boolean))];
    if (!itemIds.length) return;
    // Fetch stock balance and aggregate per-item across warehouses
    this.http.get<any>('/api/app/stock-balance', {
      params: { maxResultCount: '500', skipCount: '0', sorting: '' }
    }).subscribe({
      next: (res: any) => {
        const stockMap: Record<string, number> = {};
        for (const bin of (res.items ?? [])) {
          const key = bin.itemId;
          if (key && itemIds.includes(key)) {
            stockMap[key] = (stockMap[key] ?? 0) + (bin.actualQty ?? 0);
          }
        }
        this.itemStock.set(stockMap);
      },
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
      case 'delivery':
        this.openDeliverySelection();
        break;
      case 'invoice':
        this.initiateConversion('SalesInvoice', () => {
          this.conversionService.convertSalesOrderToSalesInvoice(id).subscribe({
            next: (inv) => this.router.navigate(['/sales/invoices', inv.id]),
            error: (err) => this.toaster.error(err?.error?.error?.data?.reason || err?.error?.error?.message || '::ConversionFailed'),
          });
        });
        break;
      case 'payment':
        this.router.navigate(['/accounting/payments/new'], {
          queryParams: {
            partyType: 'Customer',
            partyId: this.order!.customerId,
            againstOrderType: 'SalesOrder',
            againstOrderId: id,
            companyId: this.order!.companyId,
          }
        });
        break;
      case 'work_order':
        this.router.navigate(['/manufacturing/work-orders/new'], {
          queryParams: { salesOrderId: id, companyId: this.order!.companyId }
        });
        break;
      case 'production_plan':
        this.router.navigate(['/manufacturing/production-plans/new'], {
          queryParams: { salesOrderId: id, companyId: this.order!.companyId }
        });
        break;
      case 'material_request':
        this.conversionService.convertSalesOrderToMaterialRequest(id).subscribe({
          next: (mrId) => {
            this.toaster.success('::MaterialRequestCreated');
            this.router.navigate(['/purchasing/material-requests', mrId]);
          },
          error: (err) => this.toaster.error(err?.error?.error?.message || '::ConversionFailed'),
        });
        break;
      case 'pick_list':
        this.router.navigate(['/inventory/pick-lists/new'], {
          queryParams: { salesOrderId: id, customerId: this.order!.customerId, companyId: this.order!.companyId }
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
        this.amendmentService.amend(id).subscribe({
          next: (amendedId) => this.router.navigate(['/sales/orders', amendedId]),
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
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(this.order!.id!).subscribe({
        next: () => this.router.navigate(['/sales/orders']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    });
  }

  openSendEmailDialog(): void {
    this.emailRecipient = (this.order as any)?.customerEmail || '';
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
    this.http.post('/api/app/document-email/sales-order-email', {
      documentId: this.order!.id,
      recipientEmail: this.emailRecipient,
      ccEmails: this.emailCc ? this.emailCc.split(',').map((e: string) => e.trim()) : null,
      attachPdf: this.emailAttachPdf,
    }).subscribe({
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

  // --- Partial Delivery Selection ---

  /** Opens the per-item delivery selection panel */
  openDeliverySelection(): void {
    if (!this.order?.items?.length) return;
    const items = this.order.items
      .filter((i: any) => {
        const pending = (i.quantity ?? 0) - (i.deliveredQty ?? 0);
        return pending > 0;
      })
      .map((i: any) => ({
        itemId: i.itemId ?? i.id,
        description: i.description || i.itemName || '—',
        pendingQty: Math.max(0, (i.quantity ?? 0) - (i.deliveredQty ?? 0)),
        deliverQty: Math.max(0, (i.quantity ?? 0) - (i.deliveredQty ?? 0)), // default: deliver all pending
        selected: true,
      }));
    if (items.length === 0) {
      this.toaster.info('::AllItemsAlreadyDelivered');
      return;
    }
    this.deliverySelectionItems.set(items);
    this.showDeliverySelection.set(true);
  }

  /** Toggles select-all for delivery items */
  toggleAllDeliveryItems(checked: boolean): void {
    this.deliverySelectionItems.update(items =>
      items.map(i => ({ ...i, selected: checked, deliverQty: checked ? i.pendingQty : 0 }))
    );
  }

  /** Toggles individual delivery item selection */
  toggleDeliveryItem(index: number, checked: boolean): void {
    this.deliverySelectionItems.update(items =>
      items.map((item, i) => i === index ? { ...item, selected: checked, deliverQty: checked ? item.pendingQty : 0 } : item)
    );
  }

  /** Updates deliver qty for an item */
  updateDeliverQty(index: number, qty: number): void {
    this.deliverySelectionItems.update(items =>
      items.map((item, i) => i === index ? { ...item, deliverQty: Math.min(Math.max(0, qty), item.pendingQty), selected: qty > 0 } : item)
    );
  }

  /** Whether any items are selected for delivery */
  hasDeliverySelection(): boolean {
    return this.deliverySelectionItems().some(i => i.selected && i.deliverQty > 0);
  }

  /** Cancel partial delivery selection */
  cancelDeliverySelection(): void {
    this.showDeliverySelection.set(false);
    this.deliverySelectionItems.set([]);
  }

  /** Execute partial delivery: create DN with selected items */
  confirmPartialDelivery(): void {
    const selectedItems = this.deliverySelectionItems()
      .filter(i => i.selected && i.deliverQty > 0)
      .map(i => ({ salesOrderItemId: i.itemId, quantity: i.deliverQty }));

    if (!selectedItems.length) {
      this.toaster.warn('::NoItemsSelected');
      return;
    }

    this.isCreatingDN.set(true);
    const id = this.order!.id!;

    // Use draft link guard check before creating
    this.initiateConversion('DeliveryNote', () => {
      this.http.post<any>(`/api/app/document-conversion/convert-sales-order-to-delivery-note/${id}`, selectedItems).subscribe({
        next: (dn) => {
          this.isCreatingDN.set(false);
          this.showDeliverySelection.set(false);
          this.toaster.success('::SuccessfullyCreated');
          this.router.navigate(['/sales/delivery-notes', dn.id]);
        },
        error: (err) => {
          this.isCreatingDN.set(false);
          this.toaster.error(err?.error?.error?.data?.reason || err?.error?.error?.message || '::ConversionFailed');
        },
      });
    });
  }

  /** Quick action: deliver ALL pending items without selection */
  deliverAll(): void {
    const id = this.order!.id!;
    this.isCreatingDN.set(true);
    this.initiateConversion('DeliveryNote', () => {
      this.conversionService.convertSalesOrderToDeliveryNote(id).subscribe({
        next: (dn) => {
          this.isCreatingDN.set(false);
          this.showDeliverySelection.set(false);
          this.router.navigate(['/sales/delivery-notes', dn.id]);
        },
        error: (err) => {
          this.isCreatingDN.set(false);
          this.toaster.error(err?.error?.error?.data?.reason || err?.error?.error?.message || '::ConversionFailed');
        },
      });
    });
  }
}
