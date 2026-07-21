import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { SalesOrderAmendmentService } from '../../proxy/sales/sales-order-amendment.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { SalesOrderStore } from '../store/sales-order.store';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import type { SalesOrderDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-sales-order-detail',
  standalone: true,
  imports: [
    CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, StatusBadgeComponent, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, RouterLink],
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

  order: SalesOrderDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

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
    this.service.get(id).subscribe((result) => { this.order = result; });
  }

  onWorkflowAction(action: string): void {
    const id = this.order!.id!;
    switch (action) {
      case 'submit':
        this.store.submitOrder(id);
        this.reloadAfterAction();
        break;
      case 'delivery':
        this.conversionService.convertSalesOrderToDeliveryNote(id).subscribe((dn) => {
          this.router.navigate(['/sales/delivery-notes', dn.id]);
        });
        break;
      case 'invoice':
        this.conversionService.convertSalesOrderToSalesInvoice(id).subscribe((inv) => {
          this.router.navigate(['/sales/invoices', inv.id]);
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

  deleteOrder(): void {
    if (!confirm('Are you sure you want to delete this draft order?')) return;
    this.service.delete(this.order!.id!).subscribe({
      next: () => this.router.navigate(['/sales/orders']),
      error: () => {},
    });
  }
}
