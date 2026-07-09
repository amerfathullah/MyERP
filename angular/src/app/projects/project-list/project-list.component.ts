import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ProjectStore } from '../store/project.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationModule, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './project-list.component.html',
  styleUrls: ['./project-list.component.scss'],
})
export class ProjectListComponent implements OnInit {
  readonly store = inject(ProjectStore);
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
    return ['Open', 'Completed', 'Cancelled'][status] ?? 'Open';
  }
}
