import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ItemStore } from '../store/item.store';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-item-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    PaginationComponent],
  templateUrl: './item-list.component.html',
  styleUrls: ['./item-list.component.scss'],
})
export class ItemListComponent implements OnInit {
  readonly store = inject(ItemStore);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);
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
      companyId: this.companyContext.currentCompanyId() || undefined,
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

  createItem(): void {
    this.router.navigate(['/inventory/items/new']);
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove(id);
      }
    });
  }
}
