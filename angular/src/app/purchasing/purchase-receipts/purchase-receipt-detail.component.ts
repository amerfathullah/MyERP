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
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { PurchaseReceiptStore } from '../store/purchase-receipt.store';
import type { PurchaseReceiptDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-receipt-detail',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, MatCardModule, MatTableModule,
    MatButtonModule, MatIconModule, MatDividerModule,
  ],
  templateUrl: './purchase-receipt-detail.component.html',
  styleUrls: ['./purchase-receipt-detail.component.scss'],
})
export class PurchaseReceiptDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(PurchaseReceiptService);
  private conversionService = inject(PurchaseConversionService);
  private store = inject(PurchaseReceiptStore);
  private confirmation = inject(ConfirmationService);

  receipt: PurchaseReceiptDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  get workflowActions(): WorkflowAction[] {
    if (!this.receipt) return [];
    const actions: WorkflowAction[] = [];
    if (this.receipt.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (this.receipt.status === 'Submitted') {
      actions.push({ name: 'invoice', label: 'Make Invoice', icon: 'receipt', color: 'primary' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe(r => { this.receipt = r; });
  }

  onWorkflowAction(action: string): void {
    const id = this.receipt!.id!;
    switch (action) {
      case 'submit':
        this.store.submitReceipt(id);
        this.reloadAfterAction();
        break;
      case 'invoice':
        this.conversionService.convertPurchaseReceiptToInvoice(id).subscribe((inv) => {
          this.router.navigate(['/purchasing/invoices', inv.id]);
        });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.store.cancelReceipt(id);
            this.reloadAfterAction();
          }
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.receipt!.id!).subscribe(r => { this.receipt = r; });
    }, 500);
  }
}
