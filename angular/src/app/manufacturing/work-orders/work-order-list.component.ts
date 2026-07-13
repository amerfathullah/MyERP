import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { WorkOrderStore } from '../store/work-order.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-work-order-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, PaginationComponent],
  templateUrl: './work-order-list.component.html',
  styleUrls: ['./work-order-list.component.scss'],
})
export class WorkOrderListComponent implements OnInit {
  readonly store = inject(WorkOrderStore);
  private companyContext = inject(CompanyContextService);
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  statusFilter = '';

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.store.load({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: '',
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
      companyId: this.companyContext.currentCompanyId() || undefined,
    });
  }

  onSearch(): void {
    this.currentPage = 0;
    this.loadData();
  }

  onStatusChange(): void {
    this.currentPage = 0;
    this.loadData();
  }

  getStatusLabel(status: number): string {
    return ['Draft', 'Submitted', 'Not Started', 'In Process', 'Completed', 'Stopped', 'Cancelled'][status] ?? 'Draft';
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }
}
