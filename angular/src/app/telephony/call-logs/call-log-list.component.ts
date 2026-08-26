import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { CallLogService } from '../../proxy/telephony/call-log.service';
import { CallLogDto } from '../../proxy/telephony/models';

@Component({
  selector: 'app-call-log-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Call Logs</h5>
        <a routerLink="/telephony/call-logs/new" class="btn btn-primary btn-sm">New Call Log</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by Call ID, From, To..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Call ID</th>
              <th>From</th>
              <th>To</th>
              <th>Direction</th>
              <th class="text-center">Status</th>
              <th>Duration</th>
              <th>Start Time</th>
              <th class="text-end" style="width: 220px;">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/telephony/call-logs', item.id, 'edit']" class="fw-semibold">
                    {{ item.callId }}
                  </a>
                </td>
                <td>{{ item.from }}</td>
                <td>{{ item.to }}</td>
                <td>
                  <span class="badge" [ngClass]="item.callDirection === 0 ? 'bg-info' : 'bg-primary'">
                    {{ item.callDirection === 0 ? 'Incoming' : 'Outgoing' }}
                  </span>
                </td>
                <td class="text-center">
                  <span class="badge" [ngClass]="getStatusBadgeClass(item.status)">
                    {{ getStatusName(item.status) }}
                  </span>
                </td>
                <td>{{ item.duration }}s</td>
                <td>{{ item.startTime | date:'short' }}</td>
                <td class="text-end">
                  @if (item.status === 0) {
                    <button class="btn btn-sm btn-outline-success me-1" (click)="startCall(item)">Answer</button>
                  }
                  @if (item.status === 1) {
                    <button class="btn btn-sm btn-outline-warning me-1" (click)="completeCall(item)">End</button>
                  }
                  <a [routerLink]="['/telephony/call-logs', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-1">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="8" class="text-center text-muted py-4">No call logs found.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class CallLogListComponent implements OnInit {
  private service = inject(CallLogService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: CallLogDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  getStatusName(status: number): string {
    switch (status) {
      case 0: return 'Ringing';
      case 1: return 'In Progress';
      case 2: return 'Completed';
      case 3: return 'Failed';
      case 4: return 'Busy';
      case 5: return 'No Answer';
      case 6: return 'Queued';
      case 7: return 'Cancelled';
      default: return 'Unknown';
    }
  }

  getStatusBadgeClass(status: number): string {
    switch (status) {
      case 0: return 'bg-info';
      case 1: return 'bg-primary';
      case 2: return 'bg-success';
      case 3: return 'bg-danger';
      case 4: return 'bg-warning text-dark';
      case 5: return 'bg-secondary';
      default: return 'bg-dark';
    }
  }

  startCall(item: CallLogDto): void {
    this.service.startCall(item.id!).subscribe({
      next: () => {
        this.toaster.success('Call answered');
        this.load();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }

  completeCall(item: CallLogDto): void {
    this.service.completeCall(item.id!, 60).subscribe({
      next: () => {
        this.toaster.success('Call completed');
        this.load();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }

  delete(item: CallLogDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(item.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          this.load();
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    });
  }
}
