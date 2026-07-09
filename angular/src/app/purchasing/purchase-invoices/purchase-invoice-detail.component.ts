import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute } from '@angular/router';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { PurchaseInvoiceStore } from '../store/purchase-invoice.store';
import type { PurchaseInvoiceDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-invoice-detail',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe,
    DocumentWorkflowComponent, LhdnStatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './purchase-invoice-detail.component.html',
  styleUrls: ['./purchase-invoice-detail.component.scss'],
})
export class PurchaseInvoiceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(PurchaseInvoiceService);
  private store = inject(PurchaseInvoiceStore);
  private confirmation = inject(ConfirmationService);

  invoice: PurchaseInvoiceDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  get workflowActions(): WorkflowAction[] {
    if (!this.invoice) return [];
    const actions: WorkflowAction[] = [];
    if (this.invoice.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (this.invoice.status === 'Submitted') {
      actions.push({ name: 'post', label: 'Post', icon: 'verified', color: 'primary' });
    }
    if (this.invoice.status === 'Posted') {
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => { this.invoice = result; });
  }

  onWorkflowAction(action: string): void {
    const id = this.invoice!.id!;
    switch (action) {
      case 'submit':
        this.store.submitInvoice(id);
        this.reloadAfterAction();
        break;
      case 'post':
        this.store.postInvoice(id);
        this.reloadAfterAction();
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelInvoice(id);
            this.reloadAfterAction();
          }
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.invoice!.id!).subscribe((r) => { this.invoice = r; });
    }, 500);
  }
}
