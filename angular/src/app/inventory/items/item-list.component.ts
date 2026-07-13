import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ItemStore } from '../store/item.store';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-item-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    PageModule,
    LocalizationPipe,
    StatusBadgeComponent,
    PaginationComponent],
  templateUrl: './item-list.component.html',
  styleUrls: ['./item-list.component.scss'],
})
export class ItemListComponent implements OnInit {
  readonly store = inject(ItemStore);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);
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

  createItem(): void {
    this.router.navigate(['/inventory/items/new']);
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove(id);
      }
    });
  }
}
