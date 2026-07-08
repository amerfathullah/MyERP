import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { PurchaseReceiptStore } from '../store/purchase-receipt.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-purchase-receipt-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationModule, MatCardModule, MatTableModule, MatPaginatorModule, MatButtonModule, MatIconModule, MatMenuModule, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './purchase-receipt-list.component.html',
  styleUrls: ['./purchase-receipt-list.component.scss'],
})
export class PurchaseReceiptListComponent implements OnInit {
  readonly store = inject(PurchaseReceiptStore);
  displayedColumns = ['receiptNumber', 'postingDate', 'grandTotal', 'status', 'actions'];

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: 'postingDate DESC' });
  }

  onPageChange(event: PageEvent): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: 'postingDate DESC',
    });
  }
}
