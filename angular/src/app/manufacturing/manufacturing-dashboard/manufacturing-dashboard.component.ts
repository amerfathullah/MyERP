import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';

import { ProductionScheduleComponent } from '../production-schedule/production-schedule.component';
import { MaterialShortageSummaryComponent } from '../material-shortage-summary/material-shortage-summary.component';

interface WoStatusGroup {
  status: string;
  statusKey: string;
  count: number;
  totalQty: number;
  producedQty: number;
  color: string;
  icon: string;
  orders: WoBoardItem[];
}

interface WoBoardItem {
  id: string;
  orderNumber: string;
  itemName: string;
  quantity: number;
  producedQuantity: number;
  percentComplete: number;
  plannedStartDate: string | null;
  status: number;
  isOverdue: boolean;
}

interface ManufacturingKpi {
  totalActiveOrders: number;
  producedThisMonth: number;
  pendingMaterialTransfer: number;
  overdueOrders: number;
  avgCompletionRate: number;
}

interface MaterialReadiness {
  workOrderId: string;
  workOrderNumber: string;
  itemName: string;
  isReady: boolean;
  isPartial: boolean;
  hasShortage: boolean;
  totalMaterials: number;
  materialsAvailable: number;
  materialsShort: number;
  totalShortageValue: number;
  readinessStatus: string;
}

@Component({
  selector: 'app-manufacturing-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, ProductionScheduleComponent, MaterialShortageSummaryComponent],
  template: `
    <abp-page [title]="'::ManufacturingDashboard' | abpLocalization">

      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status"></div>
        </div>
      } @else {

        <!-- KPI Cards -->
        <div class="row g-3 mb-4">
          <div class="col-md-3 col-6">
            <div class="card border-start border-primary border-4 h-100">
              <div class="card-body py-3">
                <div class="text-muted small">{{ '::ActiveOrders' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-primary">{{ kpis().totalActiveOrders }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3 col-6">
            <div class="card border-start border-success border-4 h-100">
              <div class="card-body py-3">
                <div class="text-muted small">{{ '::ProducedThisMonth' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-success">{{ kpis().producedThisMonth | number:'1.0-0' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3 col-6">
            <div class="card border-start border-warning border-4 h-100">
              <div class="card-body py-3">
                <div class="text-muted small">{{ '::PendingTransfer' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-warning">{{ kpis().pendingMaterialTransfer }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3 col-6">
            <div class="card border-start border-danger border-4 h-100">
              <div class="card-body py-3">
                <div class="text-muted small">{{ '::OverdueOrders' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-danger">{{ kpis().overdueOrders }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Avg Completion Rate Progress Bar -->
        @if (kpis().totalActiveOrders > 0) {
          <div class="card mb-4">
            <div class="card-body py-2">
              <div class="d-flex justify-content-between align-items-center mb-1">
                <span class="text-muted small">{{ '::AvgCompletionRate' | abpLocalization }}</span>
                <span class="fw-bold">{{ kpis().avgCompletionRate | number:'1.0-0' }}%</span>
              </div>
              <div class="progress" style="height: 8px">
                <div class="progress-bar" [class.bg-success]="kpis().avgCompletionRate >= 80"
                     [class.bg-primary]="kpis().avgCompletionRate >= 30 && kpis().avgCompletionRate < 80"
                     [class.bg-warning]="kpis().avgCompletionRate < 30"
                     [style.width.%]="kpis().avgCompletionRate"></div>
              </div>
            </div>
          </div>
        }

        <!-- Status Pipeline Board -->
        <div class="row g-3 mb-4">
          @for (group of statusGroups(); track group.status) {
            <div class="col-md-4 col-lg-3">
              <div class="card h-100">
                <div class="card-header d-flex justify-content-between align-items-center py-2"
                     [class]="'bg-' + group.color + ' bg-opacity-10'">
                  <span>
                    <i class="fa {{ group.icon }} me-1"></i>
                    {{ group.statusKey | abpLocalization }}
                  </span>
                  <span class="badge rounded-pill" [class]="'bg-' + group.color">{{ group.count }}</span>
                </div>
                <div class="card-body p-2" style="max-height: 400px; overflow-y: auto">
                  @for (wo of group.orders; track wo.id) {
                    <a [routerLink]="['/manufacturing/work-orders', wo.id]"
                       class="card mb-2 text-decoration-none border-start border-3"
                       [class.border-danger]="wo.isOverdue"
                       [class.border-primary]="!wo.isOverdue">
                      <div class="card-body p-2">
                        <div class="d-flex justify-content-between align-items-start">
                          <div>
                            <div class="fw-semibold small">{{ wo.orderNumber || '—' }}</div>
                            <div class="text-muted" style="font-size: 0.75rem">{{ wo.itemName || '—' }}</div>
                          </div>
                          @if (wo.isOverdue) {
                            <span class="badge bg-danger" style="font-size: 0.65rem">{{ '::Overdue' | abpLocalization }}</span>
                          }
                        </div>
                        <div class="progress mt-2" style="height: 4px">
                          <div class="progress-bar" [class.bg-success]="wo.percentComplete >= 100"
                               [class.bg-primary]="wo.percentComplete > 0 && wo.percentComplete < 100"
                               [style.width.%]="wo.percentComplete"></div>
                        </div>
                        <div class="d-flex justify-content-between mt-1" style="font-size: 0.7rem">
                          <span class="text-muted">{{ wo.producedQuantity }}/{{ wo.quantity }}</span>
                          <span class="fw-bold">{{ wo.percentComplete | number:'1.0-0' }}%</span>
                        </div>
                      </div>
                    </a>
                  }
                  @if (group.orders.length === 0) {
                    <div class="text-center text-muted py-3 small">
                      <i class="fa fa-inbox d-block mb-1"></i>
                      {{ '::NoOrders' | abpLocalization }}
                    </div>
                  }
                </div>
              </div>
            </div>
          }
        </div>

        <!-- Material Readiness Section -->
        @if (materialReadiness().length > 0) {
          <div class="card mb-4">
            <div class="card-header d-flex justify-content-between align-items-center">
              <span><i class="fa fa-boxes-stacked me-2"></i>{{ '::MaterialReadiness' | abpLocalization }}</span>
              <div class="d-flex gap-2">
                <span class="badge bg-success">{{ readyCount() }} {{ '::Ready' | abpLocalization }}</span>
                <span class="badge bg-warning text-dark">{{ partialCount() }} {{ '::Partial' | abpLocalization }}</span>
                <span class="badge bg-danger">{{ blockedCount() }} {{ '::Blocked' | abpLocalization }}</span>
              </div>
            </div>
            <div class="card-body p-0">
              <div class="table-responsive">
                <table class="table table-hover table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::WorkOrderNumber' | abpLocalization }}</th>
                      <th>{{ '::Item' | abpLocalization }}</th>
                      <th class="text-center">{{ '::Materials' | abpLocalization }}</th>
                      <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (mr of materialReadiness(); track mr.workOrderId) {
                      <tr [class.table-danger]="mr.hasShortage && !mr.isPartial"
                          [class.table-warning]="mr.isPartial">
                        <td>
                          <a [routerLink]="['/manufacturing/work-orders', mr.workOrderId]" class="text-decoration-none">
                            {{ mr.workOrderNumber }}
                          </a>
                        </td>
                        <td class="small">{{ mr.itemName }}</td>
                        <td class="text-center">
                          <span class="text-success fw-bold">{{ mr.materialsAvailable }}</span>
                          <span class="text-muted">/{{ mr.totalMaterials }}</span>
                          @if (mr.materialsShort > 0) {
                            <span class="ms-1 text-danger small">({{ mr.materialsShort }} {{ '::Short' | abpLocalization }})</span>
                          }
                        </td>
                        <td class="text-center">
                          @if (mr.isReady) {
                            <span class="badge bg-success"><i class="fa fa-check me-1"></i>{{ '::Ready' | abpLocalization }}</span>
                          } @else if (mr.isPartial) {
                            <span class="badge bg-warning text-dark"><i class="fa fa-clock me-1"></i>{{ '::Partial' | abpLocalization }}</span>
                          } @else {
                            <span class="badge bg-danger"><i class="fa fa-exclamation-triangle me-1"></i>{{ '::Blocked' | abpLocalization }}</span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }

        <!-- Production Summary Table -->
        @if (allOrders().length > 0) {
          <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
              <span><i class="fa fa-table me-2"></i>{{ '::ActiveWorkOrders' | abpLocalization }}</span>
              <span class="badge bg-primary">{{ allOrders().length }}</span>
            </div>
            <div class="card-body p-0">
              <div class="table-responsive">
                <table class="table table-hover table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::WorkOrderNumber' | abpLocalization }}</th>
                      <th>{{ '::Item' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Produced' | abpLocalization }}</th>
                      <th>{{ '::Progress' | abpLocalization }}</th>
                      <th>{{ '::PlannedStart' | abpLocalization }}</th>
                      <th>{{ '::Status' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (wo of allOrders(); track wo.id) {
                      <tr [class.table-danger]="wo.isOverdue" class="cursor-pointer" [routerLink]="['/manufacturing/work-orders', wo.id]">
                        <td><a [routerLink]="['/manufacturing/work-orders', wo.id]" class="text-decoration-none">{{ wo.orderNumber || '—' }}</a></td>
                        <td>{{ wo.itemName || '—' }}</td>
                        <td class="text-end font-monospace">{{ wo.quantity | number:'1.0-2' }}</td>
                        <td class="text-end font-monospace">{{ wo.producedQuantity | number:'1.0-2' }}</td>
                        <td style="min-width: 120px">
                          <div class="d-flex align-items-center gap-2">
                            <div class="progress flex-grow-1" style="height: 6px">
                              <div class="progress-bar" [class.bg-success]="wo.percentComplete >= 100"
                                   [class.bg-primary]="wo.percentComplete > 0 && wo.percentComplete < 100"
                                   [style.width.%]="wo.percentComplete"></div>
                            </div>
                            <small class="text-muted">{{ wo.percentComplete | number:'1.0-0' }}%</small>
                          </div>
                        </td>
                        <td class="small">{{ wo.plannedStartDate ? (wo.plannedStartDate | date:'dd/MM/yyyy') : '—' }}</td>
                        <td><span class="badge" [class]="getStatusBadgeClass(wo.status)">{{ getStatusLabel(wo.status) }}</span></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }

      }
        <!-- Production Schedule Timeline -->
        <div class="row mb-4">
          <div class="col-md-8">
            <app-production-schedule />
          </div>
          <div class="col-md-4">
            <app-material-shortage-summary />
          </div>
        </div>

    </abp-page>
  `,
  styles: [`
    .cursor-pointer { cursor: pointer; }
    .cursor-pointer:hover { background-color: rgba(0,0,0,0.02); }
  `],
})
export class ManufacturingDashboardComponent implements OnInit {
  private mfgService = inject(ManufacturingService);
  private companyContext = inject(CompanyContextService);
  private l = inject(LocalizationService);

  isLoading = signal(true);
  rawOrders = signal<WoBoardItem[]>([]);
  materialReadiness = signal<MaterialReadiness[]>([]);

  kpis = computed<ManufacturingKpi>(() => {
    const orders = this.rawOrders();
    const active = orders.filter(o => [2, 3, 5].includes(o.status)); // NotStarted, InProcess, Stopped
    const now = new Date();
    const monthStart = new Date(now.getFullYear(), now.getMonth(), 1);
    const produced = orders
      .filter(o => o.status === 4) // Completed
      .reduce((s, o) => s + o.producedQuantity, 0);
    const pending = orders.filter(o => o.status === 2).length; // NotStarted = pending transfer
    const overdue = orders.filter(o => o.isOverdue).length;
    const avgRate = active.length > 0
      ? active.reduce((s, o) => s + o.percentComplete, 0) / active.length
      : 0;

    return {
      totalActiveOrders: active.length,
      producedThisMonth: produced,
      pendingMaterialTransfer: pending,
      overdueOrders: overdue,
      avgCompletionRate: avgRate,
    };
  });

  statusGroups = computed<WoStatusGroup[]>(() => {
    const orders = this.rawOrders();
    const groups: WoStatusGroup[] = [
      { status: 'NotStarted', statusKey: '::NotStarted', count: 0, totalQty: 0, producedQty: 0, color: 'secondary', icon: 'fa-clock', orders: [] },
      { status: 'InProcess', statusKey: '::InProcess', count: 0, totalQty: 0, producedQty: 0, color: 'primary', icon: 'fa-gears', orders: [] },
      { status: 'Completed', statusKey: '::Completed', count: 0, totalQty: 0, producedQty: 0, color: 'success', icon: 'fa-check-circle', orders: [] },
      { status: 'Stopped', statusKey: '::Stopped', count: 0, totalQty: 0, producedQty: 0, color: 'warning', icon: 'fa-pause-circle', orders: [] },
    ];
    const statusToIndex: Record<number, number> = { 2: 0, 3: 1, 4: 2, 5: 3 };
    for (const wo of orders) {
      const idx = statusToIndex[wo.status];
      if (idx !== undefined) {
        groups[idx].count++;
        groups[idx].totalQty += wo.quantity;
        groups[idx].producedQty += wo.producedQuantity;
        groups[idx].orders.push(wo);
      }
    }
    return groups;
  });

  allOrders = computed(() =>
    this.rawOrders()
      .filter(o => [2, 3, 5].includes(o.status))
      .sort((a, b) => {
        if (a.isOverdue && !b.isOverdue) return -1;
        if (!a.isOverdue && b.isOverdue) return 1;
        return (a.percentComplete ?? 0) - (b.percentComplete ?? 0);
      })
  );

  readyCount = computed(() => this.materialReadiness().filter(m => m.isReady).length);
  partialCount = computed(() => this.materialReadiness().filter(m => m.isPartial).length);
  blockedCount = computed(() => this.materialReadiness().filter(m => m.hasShortage && !m.isPartial).length);

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { maxResultCount: 200, skipCount: 0, sorting: 'creationTime desc' };
    if (companyId) params.companyId = companyId;

    this.mfgService.getWorkOrderList({ maxResultCount: 200, skipCount: 0, sorting: 'creationTime desc', companyId } as any).subscribe({
      next: (res: any) => {
        const today = new Date().toISOString().substring(0, 10);
        const items: WoBoardItem[] = (res.items ?? []).map((wo: any) => ({
          id: wo.id,
          orderNumber: wo.orderNumber ?? wo.workOrderNumber ?? '—',
          itemName: wo.itemName ?? wo.itemId ?? '—',
          quantity: wo.quantity ?? 0,
          producedQuantity: wo.producedQuantity ?? 0,
          percentComplete: wo.percentComplete ?? (wo.quantity > 0 ? ((wo.producedQuantity ?? 0) / wo.quantity * 100) : 0),
          plannedStartDate: wo.plannedStartDate,
          status: wo.status ?? 0,
          isOverdue: wo.plannedStartDate && wo.plannedStartDate < today && [2, 3].includes(wo.status ?? 0),
        }));
        this.rawOrders.set(items);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });

    // Load material readiness for dashboard
    const readinessParams: any = {};
    if (companyId) readinessParams.companyId = companyId;
    this.mfgService.getBatchMaterialReadiness(companyId!)
      .subscribe({ next: (data: any) => this.materialReadiness.set(data ?? []), error: () => {} });
  }

  getStatusLabel(status: number): string {
    const keys = ['::Draft', '::Submitted', '::NotStarted', '::InProcess', '::Completed', '::Stopped', '::Cancelled'];
    return this.l.instant(keys[status] ?? '::Draft');
  }

  getStatusBadgeClass(status: number): string {
    return ['bg-secondary', 'bg-info', 'bg-secondary', 'bg-primary', 'bg-success', 'bg-warning text-dark', 'bg-danger'][status] ?? 'bg-secondary';
  }
}
