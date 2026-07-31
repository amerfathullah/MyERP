import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-production-schedule',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <span class="fw-bold"><i class="fa fa-calendar-days me-2"></i>{{ '::ProductionSchedule' | abpLocalization }}</span>
        <div class="d-flex align-items-center gap-2">
          @if (schedule()?.overdue) {
            <span class="badge bg-danger">{{ schedule()!.overdue }} {{ '::OverdueOrders' | abpLocalization }}</span>
          }
          <div class="btn-group btn-group-sm">
            <button class="btn" [class.btn-primary]="viewMode() === 'timeline'" [class.btn-outline-primary]="viewMode() !== 'timeline'" (click)="viewMode.set('timeline')">
              <i class="fa fa-bars-staggered"></i>
            </button>
            <button class="btn" [class.btn-primary]="viewMode() === 'gantt'" [class.btn-outline-primary]="viewMode() !== 'gantt'" (click)="viewMode.set('gantt')">
              <i class="fa fa-chart-gantt"></i>
            </button>
          </div>
        </div>
      </div>
      <div class="card-body">
        @if (isLoading()) {
          <div class="text-center py-3"><span class="spinner-border spinner-border-sm"></span></div>
        } @else if (schedule()?.items?.length) {
          <!-- KPI row -->
          <div class="row g-2 mb-3">
            <div class="col-3">
              <div class="text-center">
                <div class="fw-bold text-primary fs-4">{{ schedule()!.totalOrders }}</div>
                <small class="text-muted">{{ '::TotalOrders' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-3">
              <div class="text-center">
                <div class="fw-bold text-warning fs-4">{{ schedule()!.notStarted }}</div>
                <small class="text-muted">{{ '::NotStarted' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-3">
              <div class="text-center">
                <div class="fw-bold text-info fs-4">{{ schedule()!.inProcess }}</div>
                <small class="text-muted">{{ '::InProcess' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-3">
              <div class="text-center">
                <div class="fw-bold text-success fs-4">{{ schedule()!.completed }}</div>
                <small class="text-muted">{{ '::Completed' | abpLocalization }}</small>
              </div>
            </div>
          </div>

          @if (viewMode() === 'gantt') {
            <!-- Gantt Timeline View (date-proportional bars per ERPNext PR #57634) -->
            <div class="gantt-timeline mb-3">
              <div class="gantt-header d-flex border-bottom pb-1 mb-2">
                <div style="width: 180px;" class="small fw-medium text-muted">{{ '::WorkOrder' | abpLocalization }}</div>
                <div class="flex-grow-1 d-flex justify-content-between small text-muted">
                  @for (label of ganttDateLabels(); track label) {
                    <span>{{ label }}</span>
                  }
                </div>
              </div>
              @for (item of ganttItems(); track item.workOrderId) {
                <div class="gantt-row d-flex align-items-center mb-1" [class.bg-danger-subtle]="item.isOverdue">
                  <div style="width: 180px;" class="pe-2">
                    <a [routerLink]="['/manufacturing/work-orders', item.workOrderId]"
                       class="text-decoration-none small fw-medium text-truncate d-block" style="max-width: 170px;">
                      {{ item.workOrderNumber }}
                    </a>
                    <div class="text-muted" style="font-size: 10px;">{{ item.itemName | slice:0:20 }}</div>
                  </div>
                  <div class="flex-grow-1 position-relative" style="height: 24px;">
                    <div class="gantt-bar position-absolute rounded-pill"
                         [class]="'bg-' + item.statusColor"
                         [style.left.%]="item.ganttLeft"
                         [style.width.%]="item.ganttWidth"
                         style="height: 18px; top: 3px; min-width: 4px; opacity: 0.85;"
                         [title]="item.workOrderNumber + ': ' + (item.percentComplete | number:'1.0-0') + '%'">
                      @if (item.ganttWidth > 12) {
                        <span class="text-white d-block text-center" style="font-size: 9px; line-height: 18px;">
                          {{ item.percentComplete | number:'1.0-0' }}%
                        </span>
                      }
                    </div>
                    <!-- Today marker -->
                    @if (todayPosition() > 0 && todayPosition() < 100) {
                      <div class="position-absolute border-start border-danger border-2" style="height: 100%; top: 0;"
                           [style.left.%]="todayPosition()"></div>
                    }
                  </div>
                </div>
              }
              <!-- Legend -->
              <div class="d-flex gap-3 mt-2 pt-2 border-top small text-muted">
                <span><span class="badge bg-success">&nbsp;</span> {{ '::Completed' | abpLocalization }}</span>
                <span><span class="badge bg-warning">&nbsp;</span> {{ '::InProcess' | abpLocalization }}</span>
                <span><span class="badge bg-danger">&nbsp;</span> {{ '::Overdue' | abpLocalization }}</span>
                <span class="ms-auto"><span class="border-start border-danger border-2 pe-1">&nbsp;</span> {{ '::Today' | abpLocalization }}</span>
              </div>
            </div>
          } @else {

          <!-- Timeline bars (list view) -->
          <div class="schedule-timeline">
            @for (item of schedule()!.items; track item.workOrderId) {
              <div class="timeline-row d-flex align-items-center mb-2 py-1 px-2 rounded"
                   [class.bg-danger-subtle]="item.isOverdue"
                   [class.border-start]="true"
                   [style.border-left-color]="getBarColor(item.statusColor)"
                   style="border-left-width: 4px !important;">
                <div class="timeline-info flex-shrink-0" style="width: 200px;">
                  <a [routerLink]="['/manufacturing/work-orders', item.workOrderId]"
                     class="text-decoration-none fw-medium small">{{ item.workOrderNumber }}</a>
                  <div class="text-muted" style="font-size: 11px;">{{ item.itemName }}</div>
                </div>
                <div class="timeline-bar flex-grow-1 mx-2">
                  <div class="progress" style="height: 20px;">
                    <div class="progress-bar"
                         [class]="'bg-' + item.statusColor"
                         [style.width.%]="item.percentComplete"
                         role="progressbar">
                      @if (item.percentComplete > 15) {
                        <small>{{ item.percentComplete | number:'1.0-0' }}%</small>
                      }
                    </div>
                  </div>
                </div>
                <div class="timeline-meta flex-shrink-0 text-end" style="width: 140px;">
                  <span class="badge" [class]="'bg-' + item.statusColor">{{ item.statusLabel }}</span>
                  @if (item.isOverdue) {
                    <div class="text-danger" style="font-size: 10px;">
                      <i class="fa fa-clock"></i> {{ item.daysOverdue }}d {{ '::Overdue' | abpLocalization }}
                    </div>
                  }
                  @if (item.plannedEndDate) {
                    <div class="text-muted" style="font-size: 10px;">
                      {{ item.plannedEndDate | date:'dd MMM' }}
                    </div>
                  }
                </div>
              </div>
            }
          </div>
          } <!-- end of @else (timeline view) -->
        } @else {
          <div class="text-center text-muted py-3">
            <i class="fa fa-calendar-check fa-2x mb-2"></i>
            <p>{{ '::NoActiveProductionOrders' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .timeline-row { transition: background-color 0.2s; }
    .timeline-row:hover { background-color: rgba(0,0,0,0.03); }
  `]
})
export class ProductionScheduleComponent implements OnInit {
  private service = inject(ManufacturingService);
  private companyContext = inject(CompanyContextService);

  schedule = signal<any>(null);
  isLoading = signal(false);
  viewMode = signal<'timeline' | 'gantt'>('timeline');

  // Gantt timeline calculations
  ganttItems = computed(() => {
    const items = this.schedule()?.items;
    if (!items?.length) return [];
    const { rangeStart, rangeEnd } = this.getGanttRange(items);
    const totalDays = Math.max(1, (rangeEnd.getTime() - rangeStart.getTime()) / 86400000);
    return items
      .filter((i: any) => i.plannedStartDate || i.plannedEndDate)
      .map((item: any) => {
        const start = item.plannedStartDate ? new Date(item.plannedStartDate) : rangeStart;
        const end = item.plannedEndDate ? new Date(item.plannedEndDate) : new Date(start.getTime() + 7 * 86400000);
        const left = Math.max(0, (start.getTime() - rangeStart.getTime()) / 86400000 / totalDays * 100);
        const width = Math.max(2, Math.min(100 - left, (end.getTime() - start.getTime()) / 86400000 / totalDays * 100));
        return { ...item, ganttLeft: left, ganttWidth: width };
      });
  });

  ganttDateLabels = computed(() => {
    const items = this.schedule()?.items;
    if (!items?.length) return [];
    const { rangeStart, rangeEnd } = this.getGanttRange(items);
    const totalDays = (rangeEnd.getTime() - rangeStart.getTime()) / 86400000;
    const labels: string[] = [];
    const step = Math.max(7, Math.ceil(totalDays / 5));
    for (let d = 0; d <= totalDays; d += step) {
      const date = new Date(rangeStart.getTime() + d * 86400000);
      labels.push(date.toLocaleDateString('en', { day: '2-digit', month: 'short' }));
    }
    return labels;
  });

  todayPosition = computed(() => {
    const items = this.schedule()?.items;
    if (!items?.length) return 0;
    const { rangeStart, rangeEnd } = this.getGanttRange(items);
    const totalDays = (rangeEnd.getTime() - rangeStart.getTime()) / 86400000;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const daysFromStart = (today.getTime() - rangeStart.getTime()) / 86400000;
    return totalDays > 0 ? (daysFromStart / totalDays) * 100 : 0;
  });

  ngOnInit() {
    const companyId = this.companyContext.currentCompanyId();
    if (companyId) {
      this.loadSchedule(companyId);
    }
  }

  private loadSchedule(companyId: string) {
    this.isLoading.set(true);
    this.service.getProductionSchedule(companyId).subscribe({
      next: data => { this.schedule.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false)
    });
  }

  private getGanttRange(items: any[]): { rangeStart: Date; rangeEnd: Date } {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    let minDate = today;
    let maxDate = new Date(today.getTime() + 30 * 86400000);
    for (const item of items) {
      if (item.plannedStartDate) {
        const d = new Date(item.plannedStartDate);
        if (d < minDate) minDate = d;
      }
      if (item.plannedEndDate) {
        const d = new Date(item.plannedEndDate);
        if (d > maxDate) maxDate = d;
      }
    }
    return { rangeStart: minDate, rangeEnd: maxDate };
  }

  getBarColor(statusColor: string): string {
    const map: Record<string, string> = {
      secondary: '#6c757d', info: '#0dcaf0', warning: '#ffc107',
      primary: '#0d6efd', success: '#198754', danger: '#dc3545', dark: '#212529'
    };
    return map[statusColor] || '#6c757d';
  }
}
