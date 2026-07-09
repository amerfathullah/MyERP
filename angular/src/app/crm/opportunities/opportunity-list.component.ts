import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { OpportunityStore } from '../store/opportunity.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-opportunity-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './opportunity-list.component.html',
  styleUrls: ['./opportunity-list.component.scss'],
})
export class OpportunityListComponent implements OnInit {
  readonly store = inject(OpportunityStore);
  private confirmation = inject(ConfirmationService);

  searchTerm = '';

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: '' });
  }

  onSearch(event: Event): void {
    this.searchTerm = (event.target as HTMLInputElement).value;
    this.store.setFilter({ filter: this.searchTerm });
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: '' });
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove(id);
      }
    });
  }

  onPageChange(page: number): void {
    this.store.load({ skipCount: page * 20, maxResultCount: 20, sorting: '' });
  }
}
