import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { MaterialRequestStore } from '../store/material-request.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-material-request-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, PaginationComponent],
  templateUrl: './material-request-list.component.html',
  styleUrls: ['./material-request-list.component.scss'],
})
export class MaterialRequestListComponent implements OnInit {
  readonly store = inject(MaterialRequestStore);
  private confirmation = inject(ConfirmationService);
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

  getTypeLabel(type: number): string {
    return ['Purchase', 'Material Transfer', 'Material Issue', 'Manufacture'][type] ?? 'Purchase';
  }

  getStatusLabel(status: number): string {
    return ['Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled', 'Rejected'][status] ?? 'Draft';
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove(id);
      }
    });
  }
}
