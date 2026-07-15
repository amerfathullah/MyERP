import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../shared/components/status-badge/status-badge.component';
import { SupplierService } from '../proxy/purchasing/supplier.service';
import type { SupplierDto } from '../proxy/purchasing/models';

import { PaginationComponent, type PageEvent } from '../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-supplier-list',
  standalone: true,
  imports: [
    PaginationComponent, CommonModule,
    RouterModule,
    FormsModule,
    LocalizationPipe,
    PageModule,
    StatusBadgeComponent],
  templateUrl: './supplier-list.component.html',
  styleUrls: ['./supplier-list.component.scss'],
})
export class SupplierListComponent implements OnInit {
  private supplierService = inject(SupplierService);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);

  suppliers: SupplierDto[] = [];
  totalCount = 0;
  isLoading = false;
  displayedColumns = ['name', 'supplierCode', 'tin', 'phone', 'email', 'actions'];

  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  private searchTimeout: any;

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading = true;
    this.supplierService.getList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: '',
      filter: this.searchTerm || undefined,
    } as any).subscribe((res) => {
      this.suppliers = res.items ?? [];
      this.totalCount = res.totalCount ?? 0;
      this.isLoading = false;
    });
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

  createSupplier(): void {
    this.router.navigate(['/suppliers/new']);
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.supplierService.delete(id).subscribe(() => {
          this.suppliers = this.suppliers.filter(s => s.id !== id);
          this.totalCount--;
        });
      }
    });
  }
}
