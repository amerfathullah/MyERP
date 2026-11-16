import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
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
import { ProductionPlanService } from '../../proxy/manufacturing/production-plan.service';
import type { ProductionPlanSummaryDto, ProductionPlanVisualizerDto } from '../../proxy/manufacturing/models';

import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-production-plan-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, ActivityLogComponent, DocumentWorkflowComponent, DocumentConnectionsComponent],
  templateUrl: './production-plan-detail.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./production-plan-detail.component.scss'],
})
export class ProductionPlanDetailComponent implements OnInit {
  readonly store = inject(ProductionPlanStore);
  private planService = inject(ProductionPlanService);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  actionLoading = signal(false);
  downloadingMaterials = signal(false);
  summaryReport = signal<ProductionPlanSummaryDto | null>(null);
  loadingSummary = signal(false);
  visualizerData = signal<ProductionPlanVisualizerDto | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.store.loadOne(id);
      this.loadSummary(id);
      this.loadVisualizer(id);
    }
  }

  loadVisualizer(id?: string): void {
    const targetId = id ?? this.plan?.id;
    if (!targetId || !this.planService.getVisualizerData) return;
    this.planService.getVisualizerData(targetId).subscribe({
      next: res => this.visualizerData.set(res),
      error: () => {},
    });
  }

  loadSummary(id?: string): void {
    const targetId = id ?? this.plan?.id;
    if (!targetId) return;
    this.loadingSummary.set(true);
    this.planService.getSummaryReport(targetId).subscribe({
      next: (res) => {
        this.summaryReport.set(res);
        this.loadingSummary.set(false);
      },
      error: () => {
        this.loadingSummary.set(false);
      },
    });
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
    this.loadSummary();
  }

  calculateMaterials(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.calculateMaterials(this.plan.id);
    this.actionLoading.set(false);
    this.loadSummary();
  }

  generateWorkOrders(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.generateWorkOrders(this.plan.id);
    this.actionLoading.set(false);
    this.loadSummary();
  }

  generateMaterialRequests(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.generateMaterialRequests(this.plan.id);
    this.actionLoading.set(false);
    this.loadSummary();
  }

  cancel(): void {
    if (!this.plan?.id || this.actionLoading()) return;
    this.actionLoading.set(true);
    this.store.cancel(this.plan.id);
    this.actionLoading.set(false);
    this.loadSummary();
  }

  downloadRawMaterials(): void {
    if (!this.plan?.id || this.downloadingMaterials()) return;
    this.downloadingMaterials.set(true);
    this.planService.downloadRawMaterials(this.plan.id).subscribe({
      next: (rows) => {
        const headers = [
          'Item Code', 'Item Name', 'Description', 'Stock UOM', 'Warehouse',
          'Required Qty as per BOM', 'Projected Qty', 'Available Qty In Hand',
          'Ordered Qty', 'Planned Qty', 'Reserved Qty for Production',
          'Safety Stock', 'Required Qty', 'UOM',
        ];
        const escapeCell = (v: string | number | undefined): string =>
          `"${String(v ?? '').replace(/"/g, '""')}"`;
        const lines = [
          headers.map(escapeCell).join(','),
          ...rows.map((r) => [
            r.itemCode, r.itemName, r.description, r.stockUom, r.warehouse,
            r.requiredBomQty ?? 0, r.projectedQty ?? 0, r.actualQty ?? 0,
            r.orderedQty ?? 0, r.plannedQty ?? 0, r.reservedQtyForProduction ?? 0,
            r.safetyStock ?? 0, r.requiredQty ?? 0, r.uom,
          ].map(escapeCell).join(',')),
        ];
        const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${this.plan?.planNumber ?? 'production-plan'}-raw-materials.csv`;
        link.click();
        URL.revokeObjectURL(url);
        this.downloadingMaterials.set(false);
      },
      error: () => {
        this.toaster.error('Failed to download raw materials');
        this.downloadingMaterials.set(false);
      },
    });
  }
}
