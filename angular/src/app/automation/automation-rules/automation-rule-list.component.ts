import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { RouterModule } from '@angular/router';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { AutomationRuleStore } from '../store/automation-rule.store';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-automation-rule-list',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe, RouterModule, PaginationComponent],
  templateUrl: './automation-rule-list.component.html',
  styleUrls: ['./automation-rule-list.component.scss'],
})
export class AutomationRuleListComponent implements OnInit {
  readonly store = inject(AutomationRuleStore);
  private confirmation = inject(ConfirmationService);
  readonly triggerLabels: Record<number, string> = {
    0: 'Document Submitted',
    1: 'Document Approved',
    2: 'Document Posted',
    3: 'Document Cancelled',
    4: 'Payment Received',
    5: 'Stock Below Reorder',
    6: 'Invoice Overdue',
    7: 'E-Invoice Validated',
    8: 'E-Invoice Rejected',
    9: 'Approval Required',
    100: 'Daily Schedule',
    101: 'Weekly Schedule',
    102: 'Monthly Schedule',
  };

  pageSize = 10;
  currentPage = 0;

  readonly actionLabels: Record<number, string> = {
    0: 'Send Notification',
    1: 'Send Email',
    2: 'Submit to LHDN',
    3: 'Create Approval',
    4: 'Update Field',
    5: 'Create Task',
    6: 'Post to Accounting',
  };

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: this.pageSize, sorting: '' });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.store.load({
      skipCount: event.pageIndex * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: '',
    });
  }

  toggleActive(id: string): void {
    this.store.toggleActive(id);
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove(id);
      }
    });
  }
}
