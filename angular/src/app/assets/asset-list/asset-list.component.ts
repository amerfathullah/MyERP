import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { AssetStore } from '../store/asset.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-asset-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './asset-list.component.html',
  styleUrls: ['./asset-list.component.scss'],
})
export class AssetListComponent implements OnInit {
  readonly store = inject(AssetStore);
  private confirmation = inject(ConfirmationService);

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: '' });
  }

  onSearch(event: Event): void {
    const filter = (event.target as HTMLInputElement).value;
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: '', filter } as any);
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove(id);
      }
    });
  }

  getStatusLabel(status: number): string {
    return ['Draft', 'Submitted', 'Partially Depreciated', 'Fully Depreciated', 'Sold', 'Scrapped', 'In Maintenance', 'Cancelled'][status] ?? 'Draft';
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.store.load({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }); }
}