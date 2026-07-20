import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { PurchaseInvoiceStore } from '../store/purchase-invoice.store';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import type { PurchaseInvoiceDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-invoice-detail',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe,
    DocumentWorkflowComponent, LhdnStatusBadgeComponent, LoadingOverlayComponent, BreadcrumbComponent, ActivityLogComponent],
  templateUrl: './purchase-invoice-detail.component.html',
  styleUrls: ['./purchase-invoice-detail.component.scss'],
})
export class PurchaseInvoiceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private service = inject(PurchaseInvoiceService);
  private store = inject(PurchaseInvoiceStore);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  invoice: PurchaseInvoiceDto | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];
  paymentSchedule = signal<any[]>([]);

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
      actions.push({ name: 'payment', label: 'Make Payment', icon: 'payment', color: 'primary' });
      actions.push({ name: 'return', label: 'Create Return', icon: 'undo', color: 'accent' });
      if ((this.invoice as any).outstandingAmount > 0) {
        actions.push({ name: 'writeOff', label: 'Write Off', icon: 'backspace', color: 'accent' });
      }
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
    }
    if (this.invoice.status === 'Cancelled') {
      actions.push({ name: 'amend', label: 'Amend', icon: 'file_copy', color: 'primary' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.invoice = result;
      this.http.get<any[]>(`/api/app/purchase-invoice/${id}/payment-schedule`)
        .subscribe(schedule => this.paymentSchedule.set(schedule ?? []));
    });
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
      case 'payment':
        this.router.navigate(['/accounting/payments/new'], {
          queryParams: { partyType: 'Supplier', againstInvoiceType: 'PurchaseInvoice', againstInvoiceId: id }
        });
        break;
      case 'return':
        this.router.navigate(['/purchasing/invoices/new'], {
          queryParams: { returnAgainst: id }
        });
        break;
      case 'writeOff':
        this.confirmation.warn('::WriteOffConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.http.post<any>(`/api/app/purchase-invoice/${id}/write-off`, {}).subscribe({
              next: () => { this.toaster.success('Invoice written off.'); this.reloadAfterAction(); },
              error: () => {},
            });
          }
        });
        break;
      case 'amend':
        this.http.post<any>(`/api/app/purchase-invoice/${id}/amend`, {}).subscribe({
          next: (amended) => this.router.navigate(['/purchasing/invoices', amended.id]),
          error: () => {},
        });
        break;
    }
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.invoice!.id!).subscribe((r) => { this.invoice = r; });
    }, 500);
  }

  printInvoice(): void {
    window.print();
  }

  amend(): void {
    this.http.post<any>(`/api/app/purchase-invoice/${this.invoice!.id}/amend`, {}).subscribe({
      next: (amended) => {
        this.router.navigate(['/purchasing/invoices', amended.id]);
      },
    });
  }

  deleteInvoice(): void {
    if (!confirm('Are you sure you want to delete this draft invoice?')) return;
    this.http.delete(`/api/app/purchase-invoice/${this.invoice!.id}`).subscribe({
      next: () => this.router.navigate(['/purchasing/invoices']),
      error: () => {},
    });
  }
}
