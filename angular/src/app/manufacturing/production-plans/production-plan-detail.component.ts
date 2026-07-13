import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionPlanStore } from '../store/production-plan.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-production-plan-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './production-plan-detail.component.html',
  styleUrls: ['./production-plan-detail.component.scss'],
})
export class ProductionPlanDetailComponent implements OnInit {
  readonly store = inject(ProductionPlanStore);
  private route = inject(ActivatedRoute);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.store.loadOne(id);
  }

  get plan() { return this.store.selectedPlan(); }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', 'In Progress', 'Completed', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  submit(): void {
    if (this.plan?.id) this.store.submit(this.plan.id);
  }

  calculateMaterials(): void {
    if (this.plan?.id) this.store.calculateMaterials(this.plan.id);
  }

  generateWorkOrders(): void {
    if (this.plan?.id) this.store.generateWorkOrders(this.plan.id);
  }

  generateMaterialRequests(): void {
    if (this.plan?.id) this.store.generateMaterialRequests(this.plan.id);
  }

  cancel(): void {
    if (this.plan?.id) this.store.cancel(this.plan.id);
  }
}
