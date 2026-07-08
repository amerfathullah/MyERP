import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatMenuModule } from '@angular/material/menu';
import { RouterModule } from '@angular/router';
import { ApprovalWorkflowStore } from '../store/approval-workflow.store';

@Component({
  selector: 'app-approval-rules-list',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, MatCardModule, MatTableModule,
    MatButtonModule, MatIconModule, MatChipsModule, MatSlideToggleModule,
    MatPaginatorModule, MatMenuModule, RouterModule,
  ],
  templateUrl: './approval-rules-list.component.html',
  styleUrls: ['./approval-rules-list.component.scss'],
})
export class ApprovalRulesListComponent implements OnInit {
  readonly store = inject(ApprovalWorkflowStore);
  displayedColumns = ['name', 'documentType', 'level', 'approver', 'minimumAmount', 'isActive', 'actions'];

  ngOnInit(): void {
    this.store.loadRules({ skipCount: 0, maxResultCount: 50, sorting: '' });
  }

  onPageChange(event: PageEvent): void {
    this.store.loadRules({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: '',
    });
  }
}
