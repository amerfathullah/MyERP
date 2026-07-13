import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { JobCardService, type JobCardDto } from '../../proxy/sales/sales-advanced.service';

@Component({
  selector: 'app-job-card-detail', standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'JobCards' | abpLocalization">
      @if (d) {
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
            <div class="col-md-3">@if (d.startedAt) { <strong>Started:</strong> {{ d.startedAt | date:'dd/MM/yyyy HH:mm' }} }</div>
            <div class="col-md-3">@if (d.completedAt) { <strong>Completed:</strong> {{ d.completedAt | date:'dd/MM/yyyy HH:mm' }} }</div>
          </div>
          @if (d.status === 0 || d.status === 1) {
            <div class="mt-3 d-flex gap-2">
              @if (d.status === 0) { <button class="btn btn-sm btn-primary" (click)="action('start')"><i class="fa fa-play me-1"></i>Start</button> }
              @if (d.status === 1) { <button class="btn btn-sm btn-success" (click)="action('complete')"><i class="fa fa-check me-1"></i>Complete</button> }
              @if (d.status === 1) { <button class="btn btn-sm btn-warning" (click)="action('hold')"><i class="fa fa-pause me-1"></i>Hold</button> }
            </div>
          }
        </div></div>

        @if ((d.timeLogs ?? []).length > 0) {
          <div class="card"><div class="card-body">
            <h6>{{ 'TimeLogs' | abpLocalization }}</h6>
            <table class="table table-sm">
              <thead><tr><th>From</th><th>To</th><th class="text-end">Minutes</th><th class="text-end">{{ 'CompletedQty' | abpLocalization }}</th></tr></thead>
              <tbody>
                @for (log of d.timeLogs; track log.id) {
                  <tr>
                    <td>{{ log.fromTime | date:'dd/MM HH:mm' }}</td>
                    <td>{{ log.toTime | date:'dd/MM HH:mm' }}</td>
                    <td class="text-end">{{ log.timeInMins | number:'1.0-0' }}</td>
                    <td class="text-end">{{ log.completedQty }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div></div>
        }
      }
    </abp-page>
  `,
})
export class JobCardDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(JobCardService);
  d: JobCardDto | null = null;

  ngOnInit() { this.load(); }

  load() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
  }

  action(type: string) {
    const id = this.route.snapshot.paramMap.get('id')!;
    const actions: Record<string, (id: string) => any> = {
      start: (i) => this.service.start(i),
      complete: (i) => this.service.complete(i),
      cancel: (i) => this.service.cancel(i),
    };
    (actions[type]?.(id) ?? this.service.get(id)).subscribe(() => this.load());
  }

  statusLabel(s: number) { return ['Open', 'Work In Progress', 'Material Transferred', 'Completed', 'On Hold', 'Cancelled'][s]; }
  statusClass(s: number) { return ['bg-secondary', 'bg-primary', 'bg-info', 'bg-success', 'bg-warning', 'bg-danger'][s]; }
}
