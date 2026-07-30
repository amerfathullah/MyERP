import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionPlanStore } from '../store/production-plan.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';

@Component({
  selector: 'app-production-plan-list',
  standalone: true,
  imports: [PaginationComponent, SortableHeaderComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, StatusBadgeComponent],
  templateUrl: './production-plan-list.component.html',
  styleUrls: ['./production-plan-list.component.scss'],
})
export class ProductionPlanListComponent implements OnInit {
  readonly store = inject(ProductionPlanStore);
  searchTerm = '';
  statusFilter = '';
  sortField: string | null = 'postingDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.store.load({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : 'postingDate DESC',
      filter: this.searchTerm || undefined,
      status: this.statusFilter ? Number(this.statusFilter) as any : undefined,
    });
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.currentPage = 0;
    this.loadData();
  }

  onStatusChange(): void { this.currentPage = 0; this.loadData(); }

  onSort(event: SortEvent): void {
    this.sortField = event.field;
    this.sortDirection = event.direction;
    this.currentPage = 0;
    this.loadData();
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', 'In Progress', 'Completed', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
