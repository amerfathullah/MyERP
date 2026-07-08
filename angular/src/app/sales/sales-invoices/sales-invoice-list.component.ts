import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
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
  displayedColumns = ['invoiceNumber', 'issueDate', 'customerName', 'grandTotal', 'status', 'eInvoiceStatus', 'actions'];

  ngOnInit(): void {
    // TODO: Wire up to SalesInvoiceAppService proxy via rxMethod
  }

  createInvoice(): void {
    // TODO: Navigate to create form
  }

  submit(id: string): void {
    // TODO: Call SalesInvoiceAppService.submit(id)
  }

  post(id: string): void {
    // TODO: Call SalesInvoiceAppService.post(id)
  }

  cancel(id: string): void {
    // TODO: Call SalesInvoiceAppService.cancel(id)
  }
}
