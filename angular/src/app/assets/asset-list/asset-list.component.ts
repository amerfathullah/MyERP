import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { AssetStore } from '../store/asset.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-asset-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './asset-list.component.html',
  styleUrls: ['./asset-list.component.scss'],
})
export class AssetListComponent implements OnInit {
  readonly store = inject(AssetStore);
  private confirmation = inject(ConfirmationService);

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
}
