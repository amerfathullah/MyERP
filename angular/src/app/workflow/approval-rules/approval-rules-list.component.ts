import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { RouterModule } from '@angular/router';
import { ApprovalWorkflowStore } from '../store/approval-workflow.store';

@Component({
  selector: 'app-approval-rules-list',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, RouterModule],
  templateUrl: './approval-rules-list.component.html',
  styleUrls: ['./approval-rules-list.component.scss'],
})
export class ApprovalRulesListComponent implements OnInit {
  readonly store = inject(ApprovalWorkflowStore);
  displayedColumns = ['name', 'documentType', 'level', 'approver', 'minimumAmount', 'isActive', 'actions'];

  ngOnInit(): void {
    this.store.loadRules({ skipCount: 0, maxResultCount: 50, sorting: '' });
  }

  onPageChange(event: any): void {
    this.store.loadRules({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: '',
    });
  }
}
