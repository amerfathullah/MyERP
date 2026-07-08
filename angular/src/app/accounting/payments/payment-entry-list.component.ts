import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { PaymentEntryStore } from '../store/payment-entry.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-payment-entry-list',
  standalone: true,
  imports: [
    CommonModule, RouterModule, PageModule, LocalizationModule,
    StatusBadgeComponent],
  templateUrl: './payment-entry-list.component.html',
  styleUrls: ['./payment-entry-list.component.scss'],
})
export class PaymentEntryListComponent implements OnInit {
  readonly store = inject(PaymentEntryStore);
  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: 'paymentDate DESC' });
  }

  onPageChange(event: any): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: 'paymentDate DESC',
    });
  }
}
