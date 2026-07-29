import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { JobCardService } from '../../proxy/manufacturing/job-card.service';
import type { JobCardDto } from '../../proxy/manufacturing/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-job-card-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, ActivityLogComponent],
  template: `
    <abp-page [title]="'JobCards' | abpLocalization">
  <app-breadcrumb />
      @if (d) {
        <!-- Header KPIs -->
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-3"><strong>{{ 'Sequence' | abpLocalization }}:</strong> #{{ d.sequenceId }}</div>
            <div class="col-md-3"><strong>{{ 'Quantity' | abpLocalization }}:</strong> {{ d.forQuantity }}</div>
            <div class="col-md-3"><strong>{{ 'CompletedQty' | abpLocalization }}:</strong> {{ d.completedQty }} / {{ d.forQuantity }}</div>
            <div class="col-md-3">
              <span class="badge" [ngClass]="statusClass(d.status)">{{ statusLabel(d.status) }}</span>
            </div>
          </div>
          <div class="row mt-2">
            <div class="col-md-3"><strong>{{ 'PlannedTime' | abpLocalization }}:</strong> {{ d.plannedTimeInMins }} min</div>
            <div class="col-md-3"><strong>{{ 'ActualTime' | abpLocalization }}:</strong> {{ d.totalTimeInMins | number:'1.0-0' }} min</div>
            <div class="col-md-3">@if (d.startedAt) { <strong>{{ '::Started' | abpLocalization }}:</strong> {{ d.startedAt | date:'dd/MM/yyyy HH:mm' }} }</div>
            <div class="col-md-3">@if (d.completedAt) { <strong>{{ '::Completed' | abpLocalization }}:</strong> {{ d.completedAt | date:'dd/MM/yyyy HH:mm' }} }</div>
          </div>
          <!-- Progress bar -->
          <div class="mt-2">
            <div class="progress" style="height: 8px;">
              <div class="progress-bar" [ngClass]="progressBarClass()" [style.width.%]="progressPct()"></div>
            </div>
            <small class="text-muted">{{ progressPct() | number:'1.0-0' }}% {{ '::Completed' | abpLocalization }}</small>
          </div>
        </div></div>

        <!-- LIVE TIMER — visible when Work In Progress (status=1) -->
        @if (d.status === 1 && timerRunning()) {
          <div class="card mb-3 border-primary"><div class="card-body text-center">
            <div class="d-flex align-items-center justify-content-center gap-3">
              <i class="fa fa-stopwatch fa-2x text-primary"></i>
              <div>
                <div class="fs-1 fw-bold font-monospace text-primary">{{ timerDisplay() }}</div>
                <small class="text-muted">{{ '::ElapsedTime' | abpLocalization }}</small>
              </div>
            </div>
            <div class="mt-3 d-flex gap-2 justify-content-center">
              <button class="btn btn-success" (click)="stopTimerAndComplete()">
                <i class="fa fa-check me-1"></i>{{ '::Complete' | abpLocalization }}
              </button>
              <button class="btn btn-warning" (click)="stopTimerAndHold()">
                <i class="fa fa-pause me-1"></i>{{ '::Hold' | abpLocalization }}
              </button>
            </div>
            @if (showCompletionQty()) {
              <div class="mt-3">
                <label class="form-label">{{ '::CompletedQtyThisRun' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm w-auto d-inline-block mx-2"
                  [(ngModel)]="completionQtyInput" [min]="0" [max]="d.forQuantity - (d.completedQty ?? 0)" step="1">
                <small class="text-muted">/ {{ d.forQuantity - (d.completedQty ?? 0) }} {{ '::Remaining' | abpLocalization }}</small>
              </div>
            }
          </div></div>
        }

        <!-- Workflow Actions — when NOT timing -->
        @if (!timerRunning()) {
          @if (d.status === 0 || d.status === 1 || d.status === 4) {
            <div class="card mb-3"><div class="card-body">
              <div class="d-flex gap-2 flex-wrap">
                @if (d.status === 0) {
                  <button class="btn btn-primary" (click)="startTimer()">
                    <i class="fa fa-play me-1"></i>{{ '::Start' | abpLocalization }}
                  </button>
                }
                @if (d.status === 1) {
                  <button class="btn btn-primary" (click)="startTimer()">
                    <i class="fa fa-stopwatch me-1"></i>{{ '::StartTimer' | abpLocalization }}
                  </button>
                  <button class="btn btn-success" (click)="action('complete')">
                    <i class="fa fa-check me-1"></i>{{ '::Complete' | abpLocalization }}
                  </button>
                  <button class="btn btn-warning" (click)="action('hold')">
                    <i class="fa fa-pause me-1"></i>{{ '::Hold' | abpLocalization }}
                  </button>
                  <button class="btn btn-outline-danger" (click)="action('cancel')">
                    <i class="fa fa-ban me-1"></i>{{ '::Cancel' | abpLocalization }}
                  </button>
                }
                @if (d.status === 4) {
                  <button class="btn btn-info" (click)="resumeAndStartTimer()">
                    <i class="fa fa-play me-1"></i>{{ '::Resume' | abpLocalization }}
                  </button>
                }
              </div>
            </div></div>
          }
        }

        <!-- Time Logs Table -->
        @if ((d.timeLogs ?? []).length > 0) {
          <div class="card mb-3"><div class="card-body">
            <h6><i class="fa fa-clock me-2"></i>{{ 'TimeLogs' | abpLocalization }}
              <span class="badge bg-secondary ms-2">{{ d.timeLogs!.length }}</span>
            </h6>
            <table class="table table-sm table-hover">
              <thead><tr>
                <th>{{ '::From' | abpLocalization }}</th>
                <th>{{ '::To' | abpLocalization }}</th>
                <th class="text-end">{{ '::Minutes' | abpLocalization }}</th>
                <th class="text-end">{{ 'CompletedQty' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (log of d.timeLogs; track log.id) {
                  <tr>
                    <td>{{ log.fromTime | date:'dd/MM HH:mm' }}</td>
                    <td>{{ log.toTime | date:'dd/MM HH:mm' }}</td>
                    <td class="text-end fw-bold">{{ log.timeInMins | number:'1.0-0' }}</td>
                    <td class="text-end">{{ log.completedQty }}</td>
                  </tr>
                }
              </tbody>
              <tfoot><tr class="table-light fw-bold">
                <td colspan="2">{{ '::Total' | abpLocalization }}</td>
                <td class="text-end">{{ d.totalTimeInMins | number:'1.0-0' }} min</td>
                <td class="text-end">{{ d.completedQty }}</td>
              </tr></tfoot>
            </table>
          </div></div>
        }

        <!-- Efficiency Metrics — visible for completed or in-progress JCs with time data -->
        @if (d.totalTimeInMins && d.totalTimeInMins > 0 && d.plannedTimeInMins && d.plannedTimeInMins > 0) {
          <div class="card mb-3"><div class="card-body">
            <h6><i class="fa fa-chart-simple me-2"></i>{{ '::EfficiencyMetrics' | abpLocalization }}</h6>
            <div class="row text-center">
              <div class="col-md-4">
                <div class="fs-4 fw-bold" [ngClass]="efficiencyColor()">{{ timeEfficiency() | number:'1.0-0' }}%</div>
                <small class="text-muted">{{ '::TimeEfficiency' | abpLocalization }}</small>
              </div>
              <div class="col-md-4">
                <div class="fs-4 fw-bold">{{ qtyPerHour() | number:'1.1-1' }}</div>
                <small class="text-muted">{{ '::QtyPerHour' | abpLocalization }}</small>
              </div>
              <div class="col-md-4">
                <div class="fs-4 fw-bold" [ngClass]="d.totalTimeInMins > d.plannedTimeInMins ? 'text-danger' : 'text-success'">
                  {{ overtimeMinutes() | number:'1.0-0' }} min
                </div>
                <small class="text-muted">{{ overtimeMinutes() >= 0 ? ('::Overtime' | abpLocalization) : ('::UnderTime' | abpLocalization) }}</small>
              </div>
            </div>
          </div></div>
        }

        <app-activity-log documentType="JobCard" [documentId]="d.id!" />
      }
    </abp-page>
  `,
})
export class JobCardDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private service = inject(JobCardService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  d: JobCardDto | null = null;

  // Timer state
  timerRunning = signal(false);
  timerStartTime = signal<Date | null>(null);
  elapsedSeconds = signal(0);
  showCompletionQty = signal(false);
  completionQtyInput = 0;
  private timerInterval: ReturnType<typeof setInterval> | null = null;

  // Computed display values
  timerDisplay = computed(() => {
    const s = this.elapsedSeconds();
    const hrs = Math.floor(s / 3600);
    const mins = Math.floor((s % 3600) / 60);
    const secs = s % 60;
    return `${hrs.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  });

  progressPct = computed(() => {
    if (!this.d?.forQuantity || this.d.forQuantity === 0) return 0;
    return Math.min(100, ((this.d.completedQty ?? 0) / this.d.forQuantity) * 100);
  });

  ngOnInit() { this.load(); }

  ngOnDestroy() { this.clearTimer(); }

  load() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: (r) => this.d = r, error: () => {} });
  }

  /** Start the live timer (begins tracking elapsed time client-side) */
  startTimer(): void {
    if (this.d?.status === 0) {
      // If Open, first call backend to Start, then begin timer
      const id = this.route.snapshot.paramMap.get('id')!;
      this.service.start(id).subscribe({
        next: () => {
          this.load();
          this.beginClientTimer();
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    } else {
      // Already WIP, just start client timer for this time log
      this.beginClientTimer();
    }
  }

  /** Resume from On Hold + start timer */
  resumeAndStartTimer(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.resume(id).subscribe({
      next: () => {
        this.load();
        this.beginClientTimer();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  /** Stop timer and record completion (adds time log via backend) */
  stopTimerAndComplete(): void {
    this.showCompletionQty.set(true);
    // If user already set qty, proceed; otherwise show qty input first
    if (this.completionQtyInput > 0) {
      this.recordTimeLogAndComplete();
    }
    // Otherwise wait for user to enter qty and click complete again
  }

  /** Called when user confirms completion qty */
  recordTimeLogAndComplete(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.clearTimer();
    // Per ERPNext: time log is recorded as part of the complete action
    this.service.complete(id).subscribe({
      next: () => {
        this.toaster.success(this.l.instant('::SuccessfullyCompleted'));
        this.showCompletionQty.set(false);
        this.completionQtyInput = 0;
        this.load();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  /** Stop timer and put on hold */
  stopTimerAndHold(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.clearTimer();
    this.service.hold(id).subscribe({
      next: () => {
        this.toaster.success(this.l.instant('::SuccessfullyPaused'));
        this.load();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  private beginClientTimer(): void {
    this.timerRunning.set(true);
    this.timerStartTime.set(new Date());
    this.elapsedSeconds.set(0);
    this.completionQtyInput = Math.max(1, (this.d?.forQuantity ?? 1) - (this.d?.completedQty ?? 0));
    this.timerInterval = setInterval(() => {
      const start = this.timerStartTime();
      if (start) {
        this.elapsedSeconds.set(Math.floor((Date.now() - start.getTime()) / 1000));
      }
    }, 1000);
  }

  private clearTimer(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
    this.timerRunning.set(false);
    this.elapsedSeconds.set(0);
    this.timerStartTime.set(null);
  }

  action(type: string) {
    const id = this.route.snapshot.paramMap.get('id')!;
    const actions: Record<string, (id: string) => any> = {
      start: (i) => this.service.start(i),
      complete: (i) => this.service.complete(i),
      cancel: (i) => this.service.cancel(i),
      hold: (i) => this.service.hold(i),
      resume: (i) => this.service.resume(i),
    };
    (actions[type]?.(id) ?? this.service.get(id)).subscribe({
      next: () => {
        this.toaster.success(this.l.instant('::SuccessfullyUpdated'));
        this.load();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  // Efficiency calculations
  timeEfficiency(): number {
    if (!this.d?.plannedTimeInMins || this.d.plannedTimeInMins === 0) return 0;
    return (this.d.plannedTimeInMins / (this.d.totalTimeInMins || 1)) * 100;
  }

  qtyPerHour(): number {
    if (!this.d?.totalTimeInMins || this.d.totalTimeInMins === 0) return 0;
    return ((this.d.completedQty ?? 0) / this.d.totalTimeInMins) * 60;
  }

  overtimeMinutes(): number {
    return (this.d?.totalTimeInMins ?? 0) - (this.d?.plannedTimeInMins ?? 0);
  }

  efficiencyColor(): string {
    const eff = this.timeEfficiency();
    if (eff >= 90) return 'text-success';
    if (eff >= 70) return 'text-warning';
    return 'text-danger';
  }

  progressBarClass(): string {
    const pct = this.progressPct();
    if (pct >= 100) return 'bg-success';
    if (pct > 0) return 'bg-primary';
    return 'bg-secondary';
  }

  statusLabel(s: number | undefined) { return ['Open', 'Work In Progress', 'Material Transferred', 'Completed', 'On Hold', 'Cancelled'][s ?? 0]; }
  statusClass(s: number | undefined) { return ['bg-secondary', 'bg-primary', 'bg-info', 'bg-success', 'bg-warning', 'bg-danger'][s ?? 0]; }
}
