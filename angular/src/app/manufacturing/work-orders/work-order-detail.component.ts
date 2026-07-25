import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import type { WorkOrderDto } from '../../proxy/manufacturing/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-work-order-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, ActivityLogComponent],
  template: `
    <abp-page [title]="wo()?.workOrderNumber ?? ('Manufacturing:WorkOrders' | abpLocalization)">
  <app-breadcrumb />
      @if (isLoading()) { <app-loading-overlay /> }
      @if (wo(); as w) {
        <div class="row mb-4">
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">{{ 'Status' | abpLocalization }}</small>
                <div class="mt-1"><app-status-badge [status]="getStatus(w.status)" /></div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">{{ '::Quantity' | abpLocalization }}</small>
                <div class="fw-bold mt-1">{{ w.quantity | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">{{ 'Manufacturing:Progress' | abpLocalization }}</small>
                <div class="fw-bold mt-1">{{ w.percentComplete | number:'1.0-0' }}%</div>
                <div class="progress mt-1" style="height: 6px;">
                  <div class="progress-bar bg-success" [style.width.%]="w.percentComplete"></div>
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">Produced</small>
                <div class="fw-bold mt-1">{{ w.producedQuantity | number:'1.2-2' }} / {{ w.quantity | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
        </div>

        <div class="d-flex gap-2 mb-3">
          @if (wo()!.status === 1) {
            <button class="btn btn-primary btn-sm" (click)="start()"><i class="fa fa-play me-1"></i>Start Production</button>
          }
          @if (wo()!.status === 3) {
            <button class="btn btn-success btn-sm" (click)="recordProduction()"><i class="fa fa-check me-1"></i>Record Production</button>
            <button class="btn btn-info btn-sm" (click)="recordConsumption()"><i class="fa fa-flask me-1"></i>Record Consumption</button>
            <button class="btn btn-warning btn-sm" (click)="stop()"><i class="fa fa-pause me-1"></i>Stop</button>
          }
          @if (wo()!.status === 5) {
            <button class="btn btn-primary btn-sm" (click)="unstop()"><i class="fa fa-play me-1"></i>Resume</button>
          }
          @if (wo()!.status! >= 1 && wo()!.status! < 4) {
            <button class="btn btn-outline-secondary btn-sm" (click)="createStockEntry()"><i class="fa fa-truck me-1"></i>Material Transfer</button>
          }
          @if (wo()!.status! >= 1 && wo()!.status! <= 3) {
            <button class="btn btn-outline-danger btn-sm" (click)="cancel()"><i class="fa fa-times me-1"></i>{{ '::Cancel' | abpLocalization }}</button>
          }
        </div>

        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-4 mb-2">
                <small class="text-muted d-block">{{ 'Manufacturing:Item' | abpLocalization }}</small>
                <span>{{ w.itemName ?? w.itemId }}</span>
              </div>
              @if (w.plannedStartDate) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ 'StartDate' | abpLocalization }}</small>
                  <span>{{ w.plannedStartDate | date:'dd/MM/yyyy' }}</span>
                </div>
              }
              @if (w.plannedEndDate) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ 'EndDate' | abpLocalization }}</small>
                  <span>{{ w.plannedEndDate | date:'dd/MM/yyyy' }}</span>
                </div>
              }
            </div>
          </div>
        </div>

        @if (w.requiredItems && w.requiredItems.length > 0) {
          <div class="card">
            <div class="card-header fw-bold">Required Materials</div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>{{ 'Manufacturing:Item' | abpLocalization }}</th>
                    <th class="text-end">Required</th>
                    <th class="text-end">Transferred</th>
                    <th class="text-end">Consumed</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of w.requiredItems; track item.id) {
                    <tr>
                      <td>{{ item.itemName }}</td>
                      <td class="text-end">{{ item.requiredQuantity | number:'1.2-2' }}</td>
                      <td class="text-end">{{ item.transferredQuantity | number:'1.2-2' }}</td>
                      <td class="text-end">{{ item.consumedQuantity | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <app-activity-log documentType="WorkOrder" [documentId]="w.id!" />
      }
    </abp-page>
  `,
})
export class WorkOrderDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(ManufacturingService);
  private toaster = inject(ToasterService);
  private manufacturingService = inject(ManufacturingService);
  private stockEntryService = inject(StockEntryService);

  wo = signal<WorkOrderDto | null>(null);
  isLoading = signal(false);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading.set(true);
      this.service.getWorkOrder(id).subscribe({
        next: w => { this.wo.set(w); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
    }
  }

  start() {
    const id = this.wo()!.id!;
    this.service.startWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('Work Order started'); },
      error: () => this.toaster.error('Failed to start'),
    });
  }

  recordProduction() {
    const id = this.wo()!.id!;
    const qty = prompt('Enter produced quantity:');
    if (!qty || isNaN(+qty) || +qty <= 0) return;
    this.service.recordProduction(id, +qty).subscribe({
      next: w => { this.wo.set(w); this.toaster.success(`Recorded ${qty} units`); },
      error: () => this.toaster.error('Failed to record production'),
    });
  }

  stop() {
    const id = this.wo()!.id!;
    this.service.stopWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('Work Order stopped'); },
      error: () => this.toaster.error('Failed to stop'),
    });
  }

  unstop() {
    const id = this.wo()!.id!;
    this.manufacturingService.unstopWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('Work Order resumed'); },
      error: () => this.toaster.error('Failed to resume'),
    });
  }

  cancel() {
    if (!confirm('Are you sure you want to cancel this Work Order?')) return;
    const id = this.wo()!.id!;
    this.service.cancelWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('Work Order cancelled'); },
      error: () => this.toaster.error('Failed to cancel. Cancel all linked Stock Entries first.'),
    });
  }

  recordConsumption() {
    const wo = this.wo()!;
    const items = wo.requiredItems ?? [];
    if (!items.length) {
      this.toaster.warn('No raw materials defined for this Work Order');
      return;
    }
    if (!confirm('Record actual material consumption for this Work Order?')) return;

    // Build consumption items from WO BOM items (use transferred qty as max)
    const consumptionItems = items
      .filter((i: any) => (i.transferredQuantity ?? 0) > 0)
      .map((i: any) => ({
        itemId: i.itemId,
        quantity: i.transferredQuantity ?? i.requiredQuantity,
      }));

    if (!consumptionItems.length) {
      this.toaster.warn('No materials have been transferred yet. Transfer materials first.');
      return;
    }

    this.isLoading.set(true);
    this.manufacturingService.createMaterialConsumption({
      workOrderId: wo.id,
      items: consumptionItems,
    }).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        this.toaster.success(`Consumption recorded: ${result.itemCount} items, value ${result.totalConsumedValue?.toFixed(2)}`);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message || 'Failed to record material consumption');
      },
    });
  }

  createStockEntry() {
    const woId = this.wo()!.id!;
    if (!confirm('Create Material Transfer Stock Entry for all pending materials?')) return;
    this.isLoading.set(true);
    this.stockEntryService.createMaterialTransferForManufacture(woId).subscribe({
      next: (se) => {
        this.isLoading.set(false);
        this.toaster.success('Material Transfer created');
        this.router.navigate(['/inventory/stock-entries', se.id]);
      },
      error: () => {
        this.isLoading.set(false);
        // Fallback: navigate to manual form
        this.router.navigate(['/inventory/stock-entries/new'], {
          queryParams: { workOrderId: woId, purpose: 'MaterialTransferForManufacture' }
        });
      },
    });
  }

  getStatus(s: number | undefined): string {
    return ['Draft', 'Submitted', 'Not Started', 'In Process', 'Completed', 'Stopped', 'Cancelled'][s ?? 0] ?? 'Draft';
  }
}
