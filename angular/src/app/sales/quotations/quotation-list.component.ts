import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QuotationStore } from '../store/quotation.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-quotation-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, PaginationComponent],
  templateUrl: './quotation-list.component.html',
  styleUrls: ['./quotation-list.component.scss'],
})
export class QuotationListComponent implements OnInit {
  readonly store = inject(QuotationStore);
  private companyContext = inject(CompanyContextService);
  displayedColumns = ['quotationNumber', 'issueDate', 'grandTotal', 'status', 'actions'];
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
      sorting: 'issueDate DESC',
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

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  /** Returns true when quotation is past its validUntil date and still Submitted */
  isExpired(row: any): boolean {
    if (row.status !== 'Submitted' || !row.validUntil) return false;
    return new Date(row.validUntil) < new Date();
  }

  /** Returns true when quotation expires within 7 days */
  isExpiringSoon(row: any): boolean {
    if (row.status !== 'Submitted' || !row.validUntil) return false;
    const days = this.getDaysRemaining(row);
    return days > 0 && days <= 7;
  }

  /** Returns number of days until expiry (negative = expired) */
  getDaysRemaining(row: any): number {
    if (!row.validUntil) return 999;
    const diff = new Date(row.validUntil).getTime() - new Date().getTime();
    return Math.ceil(diff / (1000 * 60 * 60 * 24));
  }
}
