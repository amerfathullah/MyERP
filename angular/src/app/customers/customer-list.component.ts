import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { CustomerStore } from './store/customer.store';

import { PaginationComponent, type PageEvent } from '../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [
    PaginationComponent, CommonModule,
    RouterModule,
    FormsModule,
    LocalizationPipe,
    PageModule],
  templateUrl: './customer-list.component.html',
  styleUrls: ['./customer-list.component.scss'],
})
export class CustomerListComponent implements OnInit {
  readonly store = inject(CustomerStore);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  private searchTimeout: any;

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.store.load({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: '',
      filter: this.searchTerm || undefined,
    } as any);
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    clearTimeout(this.searchTimeout);
    this.searchTimeout = setTimeout(() => {
      this.currentPage = 0;
      this.loadData();
    }, 400);
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  createCustomer(): void {
    this.router.navigate(['/customers/new']);
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove(id);
      }
    });
  }
}
