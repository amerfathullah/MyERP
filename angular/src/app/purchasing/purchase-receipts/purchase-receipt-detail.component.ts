import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router } from '@angular/router';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { PurchaseReceiptStore } from '../store/purchase-receipt.store';
import type { PurchaseReceiptDto } from '../../proxy/purchasing/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import { PurchaseReceiptPrintLayoutComponent } from '../../shared/components/pr-print-layout/pr-print-layout.component';
import { CompanyService } from '../../proxy/core/company.service';

@Component({
  selector: 'app-purchase-receipt-detail',
  standalone: true,
  imports: [
    BreadcrumbComponent, CommonModule, DocumentWorkflowComponent, LoadingOverlayComponent, PageModule, LocalizationPipe, ActivityLogComponent, VoucherLedgerComponent, PurchaseReceiptPrintLayoutComponent],
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
  private companyService = inject(CompanyService);

  receipt: PurchaseReceiptDto | null = null;
  companyData = { name: '', tin: '', sst: '', address: '' };
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];

  get workflowActions(): WorkflowAction[] {
    if (!this.receipt) return [];
    const actions: WorkflowAction[] = [];
    if (this.receipt.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (this.receipt.status === 'Submitted') {
      actions.push({ name: 'invoice', label: 'Make Invoice', icon: 'receipt', color: 'primary' });
      actions.push({ name: 'return', label: 'Create Return', icon: 'rotate-left', color: 'warning' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
    }
    if (this.receipt.status === 'Cancelled') {
      actions.push({ name: 'amend', label: 'Amend', icon: 'file-circle-plus', color: 'success' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe(r => { this.receipt = r; });
    this.loadCompanyData();
  }

  printDocument(): void {
    window.print();
  }

  private loadCompanyData(): void {
    this.companyService.getList({ maxResultCount: 200, skipCount: 0, sorting: '' }).subscribe((res: any) => { const companies = res.items || [];
      if (companies?.length > 0) {
        const c = companies[0];
        this.companyData = {
          name: c.name || c.companyName || '',
          tin: c.tin || '',
          sst: c.sstRegistrationNumber || '',
          address: c.address || '',
        };
      }
    });
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
      case 'amend':
        this.service.amend(id).subscribe({
          next: (amended) => this.router.navigate(['/purchasing/receipts', amended.id]),
        });
        break;
      case 'return':
        this.router.navigate(['/purchasing/receipts/new'], {
          queryParams: { returnAgainst: id }
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
