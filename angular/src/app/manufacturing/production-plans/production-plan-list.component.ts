import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionPlanStore } from '../store/production-plan.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-production-plan-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './production-plan-list.component.html',
  styleUrls: ['./production-plan-list.component.scss'],
})
export class ProductionPlanListComponent implements OnInit {
  readonly store = inject(ProductionPlanStore);

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20 });
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', 'In Progress', 'Completed', 'Cancelled'][status ?? 0] ?? 'Draft';
  }
}
