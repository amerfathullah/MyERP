import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-purchase-receipt-list',
  standalone: true,
  imports: [CommonModule, PageModule, MatCardModule, MatTableModule, MatButtonModule, MatIconModule, StatusBadgeComponent],
  templateUrl: './purchase-receipt-list.component.html',
  styleUrls: ['./purchase-receipt-list.component.scss'],
})
export class PurchaseReceiptListComponent {
  receipts: any[] = [];
  displayedColumns = ['receiptNumber', 'date', 'supplierName', 'purchaseOrder', 'status'];
  create(): void { /* TODO */ }
}
