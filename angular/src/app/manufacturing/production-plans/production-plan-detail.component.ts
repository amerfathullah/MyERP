import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ProductionPlanStore } from '../store/production-plan.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { ToasterService } from '@abp/ng.theme.shared';

import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-production-plan-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, ActivityLogComponent, DocumentWorkflowComponent, DocumentConnectionsComponent],
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

  get workflowActions(): WorkflowAction[] {
    if (!this.plan) return [];
    const s = this.plan.status ?? 0;
    const actions: WorkflowAction[] = [];
    if (s === 0) {
      actions.push({ name: 'calculateMaterials', label: 'Calculate Materials', icon: 'calculator', color: 'info' });
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (s === 1 || s === 2) {
      actions.push({ name: 'generateWorkOrders', label: 'Generate Work Orders', icon: 'industry', color: 'primary' });
      actions.push({ name: 'generateMaterialRequests', label: 'Generate Material Requests', icon: 'boxes-stacked', color: 'warning' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  getItemProgressPct(item: any): number {
    return item.plannedQty > 0 ? Math.min(100, ((item.producedQty ?? 0) / item.plannedQty) * 100) : 0;
  }

  isShortage(mr: any): boolean {
    return (mr.requiredQty ?? 0) > (mr.availableQty ?? 0);
  }

  getStatusLabel(status: number | undefined): string {
    const keys = ['Draft', 'Submitted', 'InProcess', 'Completed', 'Cancelled'];
    return this.localization.instant('::' + (keys[status ?? 0] ?? 'Draft'));
  }

  onWorkflowAction(action: string): void {
    switch (action) {
      case 'submit': this.submit(); break;
      case 'calculateMaterials': this.calculateMaterials(); break;
      case 'generateWorkOrders': this.generateWorkOrders(); break;
      case 'generateMaterialRequests': this.generateMaterialRequests(); break;
      case 'cancel': this.cancel(); break;
    }
  }

  submit(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.submit(this.plan.id);
    this.actionLoading.set(false);
  }

  calculateMaterials(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.calculateMaterials(this.plan.id);
    this.actionLoading.set(false);
  }

  generateWorkOrders(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.generateWorkOrders(this.plan.id);
    this.actionLoading.set(false);
  }

  generateMaterialRequests(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.generateMaterialRequests(this.plan.id);
    this.actionLoading.set(false);
  }

  cancel(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.cancel(this.plan.id);
    this.actionLoading.set(false);
  }
}
