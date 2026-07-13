import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { JournalEntryStore } from '../store/journal-entry.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-journal-entry-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    PaginationComponent],
  templateUrl: './journal-entry-list.component.html',
  styleUrls: ['./journal-entry-list.component.scss'],
})
export class JournalEntryListComponent implements OnInit {
  readonly store = inject(JournalEntryStore);
  private companyContext = inject(CompanyContextService);

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: 'postingDate DESC', companyId: this.companyContext.currentCompanyId() || undefined });
  }

  onPageChange(event: any): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: 'postingDate DESC',
      companyId: this.companyContext.currentCompanyId() || undefined,
    });
  }
}
