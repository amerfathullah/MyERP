import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { SalesOrderStore } from '../store/sales-order.store';
import type { SalesOrderDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-sales-order-detail',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, MatCardModule, MatTableModule,
    MatButtonModule, MatIconModule, MatDividerModule,
    DocumentWorkflowComponent, StatusBadgeComponent, LoadingOverlayComponent,
  ],
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

  order: SalesOrderDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  get workflowActions(): WorkflowAction[] {
    if (!this.order) return [];
    const actions: WorkflowAction[] = [];
    if (this.order.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (this.order.status === 'Submitted') {
      actions.push({ name: 'delivery', label: 'Create Delivery Note', icon: 'local_shipping', color: 'primary' });
      actions.push({ name: 'invoice', label: 'Create Invoice', icon: 'receipt', color: 'accent' });
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
