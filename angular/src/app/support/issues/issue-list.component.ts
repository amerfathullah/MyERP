import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { IssueStore } from '../store/issue.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-issue-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './issue-list.component.html',
  styleUrls: ['./issue-list.component.scss'],
})
export class IssueListComponent implements OnInit {
  readonly store = inject(IssueStore);

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20 });
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
}
