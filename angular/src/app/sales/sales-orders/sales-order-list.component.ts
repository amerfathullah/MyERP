import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-sales-order-list',
  standalone: true,
  imports: [CommonModule, PageModule, MatCardModule, MatTableModule, MatButtonModule, MatIconModule, MatMenuModule, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './sales-order-list.component.html',
  styleUrls: ['./sales-order-list.component.scss'],
})
export class SalesOrderListComponent implements OnInit {
  private router = inject(Router);
  orders: any[] = [];
  isLoading = false;
  displayedColumns = ['orderNumber', 'date', 'customerName', 'deliveryDate', 'grandTotal', 'status', 'actions'];

  ngOnInit(): void {
    // TODO: Wire to SalesOrderAppService proxy
  }

  create(): void {
    this.router.navigate(['/sales/orders/new']);
  }

  makeInvoice(id: string): void {
    // TODO: Navigate to create invoice with SO reference
    this.router.navigate(['/sales/invoices/new'], { queryParams: { fromSalesOrder: id } });
  }

  makeDeliveryNote(id: string): void {
    // TODO: Navigate to create delivery note
  }
}
