import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { Confirmation } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SalesInvoiceStore } from '../store/sales-invoice.store';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-sales-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent,
    PaginationComponent],
  templateUrl: './sales-invoice-list.component.html',
  styleUrls: ['./sales-invoice-list.component.scss'],
})
export class SalesInvoiceListComponent implements OnInit {
  readonly store = inject(SalesInvoiceStore);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);
  displayedColumns = ['invoiceNumber', 'issueDate', 'customerName', 'grandTotal', 'status', 'eInvoiceStatus', 'actions'];
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';
  private searchTimeout: any;

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.store.loadInvoices({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
      companyId: this.companyContext.currentCompanyId() || undefined,
    });
  }

  createInvoice(): void {
    this.router.navigate(['/sales/invoices/new']);
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    clearTimeout(this.searchTimeout);
    this.searchTimeout = setTimeout(() => {
      this.currentPage = 0;
      this.loadData();
    }, 400);
  }

  onStatusFilter(status: string): void {
    this.statusFilter = status;
    this.currentPage = 0;
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.loadData();
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
