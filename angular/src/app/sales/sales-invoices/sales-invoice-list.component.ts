import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { SalesInvoiceStore } from '../store/sales-invoice.store';

@Component({
  selector: 'app-sales-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent,
    LoadingOverlayComponent,
  ],
  templateUrl: './sales-invoice-list.component.html',
  styleUrls: ['./sales-invoice-list.component.scss'],
})
export class SalesInvoiceListComponent implements OnInit {
  readonly store = inject(SalesInvoiceStore);
  private router = inject(Router);
  displayedColumns = ['invoiceNumber', 'issueDate', 'customerName', 'grandTotal', 'status', 'eInvoiceStatus', 'actions'];

  ngOnInit(): void {
    this.store.loadInvoices({ skipCount: 0, maxResultCount: 20 });
  }

  createInvoice(): void {
    this.router.navigate(['/sales/invoices/new']);
  }

  submit(id: string): void {
    this.store.submitInvoice(id);
  }

  post(id: string): void {
    this.store.postInvoice(id);
  }

  cancel(id: string): void {
    this.store.cancelInvoice(id);
  }
}
