import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { Confirmation } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { SalesInvoiceStore } from '../store/sales-invoice.store';

@Component({
  selector: 'app-sales-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent],
  templateUrl: './sales-invoice-list.component.html',
  styleUrls: ['./sales-invoice-list.component.scss'],
})
export class SalesInvoiceListComponent implements OnInit {
  readonly store = inject(SalesInvoiceStore);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);
  displayedColumns = ['invoiceNumber', 'issueDate', 'customerName', 'grandTotal', 'status', 'eInvoiceStatus', 'actions'];

  ngOnInit(): void {
    this.store.loadInvoices({ skipCount: 0, maxResultCount: 20 });
  }

  createInvoice(): void {
    this.router.navigate(['/sales/invoices/new']);
  }

  onPageChange(event: any): void {
    this.store.loadInvoices({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
    });
  }

  submit(id: string): void {
    this.store.submitInvoice(id);
  }

  post(id: string): void {
    this.store.postInvoice(id);
  }

  cancel(id: string): void {
    this.confirmation.warn('::CancelConfirmationMessage', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.cancelInvoice(id);
      }
    });
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.deleteInvoice(id);
      }
    });
  }
}
