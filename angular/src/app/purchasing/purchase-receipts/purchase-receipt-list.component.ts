import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { PurchaseReceiptStore } from '../store/purchase-receipt.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-purchase-receipt-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    LocalizationModule,
    PageModule,
    StatusBadgeComponent],
  templateUrl: './purchase-receipt-list.component.html',
  styleUrls: ['./purchase-receipt-list.component.scss'],
})
export class PurchaseReceiptListComponent implements OnInit {
  readonly store = inject(PurchaseReceiptStore);
  displayedColumns = ['receiptNumber', 'postingDate', 'grandTotal', 'status', 'actions'];

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: 'postingDate DESC' });
  }

  onPageChange(event: any): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: 'postingDate DESC',
    });
  }
}
