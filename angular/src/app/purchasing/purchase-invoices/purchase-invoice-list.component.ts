import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PurchaseInvoiceStore } from '../store/purchase-invoice.store';

@Component({
  selector: 'app-purchase-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    StatusBadgeComponent,
    LhdnStatusBadgeComponent,
    LoadingOverlayComponent,
  ],
  templateUrl: './purchase-invoice-list.component.html',
  styleUrls: ['./purchase-invoice-list.component.scss'],
})
export class PurchaseInvoiceListComponent implements OnInit {
  readonly store = inject(PurchaseInvoiceStore);
  private router = inject(Router);
  displayedColumns = ['invoiceNumber', 'issueDate', 'supplierName', 'grandTotal', 'status', 'eInvoiceStatus', 'actions'];

  ngOnInit(): void {
    // TODO: Wire up to PurchaseInvoiceAppService proxy
  }

  createInvoice(): void {
    this.router.navigate(['/purchasing/invoices/new']);
  }

  viewDetail(id: string): void {
    this.router.navigate(['/purchasing/invoices', id]);
  }

  submit(id: string): void {
    // TODO: Call PurchaseInvoiceAppService.submit(id)
  }

  post(id: string): void {
    // TODO: Call PurchaseInvoiceAppService.post(id)
  }

  cancel(id: string): void {
    // TODO: Call PurchaseInvoiceAppService.cancel(id)
  }
}
