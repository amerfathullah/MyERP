import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { PurchaseOrderStore } from '../store/purchase-order.store';
import type { PurchaseOrderDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-order-detail',
  standalone: true,
  imports: [
    CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, PageModule, LocalizationModule],
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

  order: PurchaseOrderDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  get workflowActions(): WorkflowAction[] {
    if (!this.order) return [];
    const actions: WorkflowAction[] = [];
    if (this.order.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (this.order.status === 'Submitted') {
      actions.push({ name: 'receipt', label: 'Make Receipt', icon: 'inventory_2', color: 'primary' });
      actions.push({ name: 'invoice', label: 'Make Invoice', icon: 'receipt', color: 'accent' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
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
      case 'receipt':
        this.conversionService.convertPurchaseOrderToReceipt(id).subscribe((receipt) => {
          this.router.navigate(['/purchasing/receipts', receipt.id]);
        });
        break;
      case 'invoice':
        this.conversionService.convertPurchaseOrderToInvoice(id).subscribe((invoice) => {
          this.router.navigate(['/purchasing/invoices', invoice.id]);
        });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelOrder(id);
            this.reloadAfterAction();
          }
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.order!.id!).subscribe((r) => { this.order = r; });
    }, 500);
  }
}
