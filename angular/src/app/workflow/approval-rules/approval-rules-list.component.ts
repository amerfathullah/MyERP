import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { RouterModule } from '@angular/router';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { ApprovalWorkflowStore } from '../store/approval-workflow.store';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import type { ApprovalRuleDto } from '../../proxy/workflow/dtos/models';

@Component({
  selector: 'app-approval-rules-list',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe, RouterModule, PaginationComponent],
  templateUrl: './approval-rules-list.component.html',
  styleUrls: ['./approval-rules-list.component.scss'],
})
export class ApprovalRulesListComponent implements OnInit {
  readonly store = inject(ApprovalWorkflowStore);
  private confirmation = inject(ConfirmationService);
  displayedColumns = ['name', 'documentType', 'level', 'approver', 'minimumAmount', 'isActive', 'actions'];
  pageSize = 10;
  currentPage = 0;

  ngOnInit(): void {
    this.store.loadRules({ skipCount: 0, maxResultCount: this.pageSize, sorting: '' });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.store.loadRules({
      skipCount: event.pageIndex * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: '',
    });
  }

  delete(rule: ApprovalRuleDto): void {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === 'confirm') {
        this.store.deleteRule(rule.id!);
      }
    });
  }
}
