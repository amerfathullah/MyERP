import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { RouterModule } from '@angular/router';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { StockEntryStore } from '../store/stock-entry.store';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-stock-entry-list',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe,
    RouterModule, StatusBadgeComponent, PaginationComponent],
  templateUrl: './stock-entry-list.component.html',
  styleUrls: ['./stock-entry-list.component.scss'],
})
export class StockEntryListComponent implements OnInit {
  readonly store = inject(StockEntryStore);
  private companyContext = inject(CompanyContextService);

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: '', companyId: this.companyContext.currentCompanyId() || undefined });
  }

  onPageChange(event: any): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: '',
      companyId: this.companyContext.currentCompanyId() || undefined,
    });
  }
}
