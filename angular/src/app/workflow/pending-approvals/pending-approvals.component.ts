import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatMenuModule } from '@angular/material/menu';
import { RouterModule } from '@angular/router';
import { ApprovalWorkflowStore } from '../store/approval-workflow.store';

@Component({
  selector: 'app-pending-approvals',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, MatCardModule, MatTableModule, MatPaginatorModule,
    MatMenuModule, RouterModule,
  ],
  templateUrl: './pending-approvals.component.html',
  styleUrls: ['./pending-approvals.component.scss'],
})
export class PendingApprovalsComponent implements OnInit {
  readonly store = inject(ApprovalWorkflowStore);
  displayedColumns = ['documentType', 'level', 'requestedBy', 'createdAt', 'actions'];

  ngOnInit(): void {
    this.store.loadPendingApprovals({ skipCount: 0, maxResultCount: 20, sorting: '' });
  }

  onPageChange(event: PageEvent): void {
    this.store.loadPendingApprovals({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: '',
    });
  }

  approve(requestId: string): void {
    this.store.approve({ requestId });
  }

  reject(requestId: string): void {
    this.store.reject({ requestId, remarks: 'Rejected' });
  }
}
