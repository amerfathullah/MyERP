import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ProductionPlanStore } from '../store/production-plan.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { ToasterService } from '@abp/ng.theme.shared';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-production-plan-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, ActivityLogComponent],
  templateUrl: './production-plan-detail.component.html',
  styleUrls: ['./production-plan-detail.component.scss'],
})
export class ProductionPlanDetailComponent implements OnInit {
  readonly store = inject(ProductionPlanStore);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  actionLoading = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.store.loadOne(id);
  }

  private localization = inject(LocalizationService);

  get plan() { return this.store.selectedPlan(); }

  getStatusLabel(status: number | undefined): string {
    const keys = ['Draft', 'Submitted', 'InProcess', 'Completed', 'Cancelled'];
    return this.localization.instant('::' + (keys[status ?? 0] ?? 'Draft'));
  }

  submit(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.submit(this.plan.id);
    setTimeout(() => this.actionLoading.set(false), 1500);
  }

  calculateMaterials(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.calculateMaterials(this.plan.id);
    setTimeout(() => this.actionLoading.set(false), 1500);
  }

  generateWorkOrders(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.generateWorkOrders(this.plan.id);
    setTimeout(() => this.actionLoading.set(false), 1500);
  }

  generateMaterialRequests(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.generateMaterialRequests(this.plan.id);
    setTimeout(() => this.actionLoading.set(false), 1500);
  }

  cancel(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.cancel(this.plan.id);
    setTimeout(() => this.actionLoading.set(false), 1500);
  }
}
