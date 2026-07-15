import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { IssueStore } from '../store/issue.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-issue-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, StatusBadgeComponent],
  templateUrl: './issue-list.component.html',
  styleUrls: ['./issue-list.component.scss'],
})
export class IssueListComponent implements OnInit {
  readonly store = inject(IssueStore);
  searchTerm = '';
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20 });
  }

  onSearch(): void {
    this.currentPage = 0;
    this.store.load({ skipCount: 0, maxResultCount: this.pageSize, filter: this.searchTerm } as any);
  }

  getStatusLabel(status: number | undefined): string {
    return ['Open', 'Replied', 'On Hold', 'Closed', 'Cancelled'][status ?? 0] ?? 'Open';
  }

  getPriorityClass(priority: string | undefined): string {
    switch (priority?.toLowerCase()) {
      case 'urgent': return 'text-danger fw-bold';
      case 'high': return 'text-warning fw-bold';
      case 'medium': return '';
      case 'low': return 'text-muted';
      default: return '';
    }
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.store.load({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }); }
}
