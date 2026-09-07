import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { BulkTransactionLogService } from '../../proxy/automation/bulk-transaction-log.service';
import { BulkTransactionLogDto, BulkTransactionLogDetailDto } from '../../proxy/automation/models';
import { BulkTransactionStatus } from '../../proxy/automation/bulk-transaction-status.enum';

@Component({
  selector: 'app-bulk-transaction-log-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    @if (log) {
      <div class="card mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div>
            <h5 class="card-title mb-0">{{ log.title }}</h5>
            <small class="text-muted">Batch Date: {{ log.batchDate | date:'medium' }}</small>
          </div>
          <a routerLink="/automation/bulk-transactions" class="btn btn-secondary btn-sm">Back to List</a>
        </div>
        <div class="card-body">
          <div class="row text-center mb-4">
            <div class="col-md-4">
              <div class="p-3 border rounded bg-light">
                <div class="h3 mb-0 text-secondary">{{ log.totalEntries }}</div>
                <small class="text-muted text-uppercase fw-semibold">Total Entries</small>
              </div>
            </div>
            <div class="col-md-4">
              <div class="p-3 border rounded bg-light">
                <div class="h3 mb-0 text-success">{{ log.succeededCount }}</div>
                <small class="text-muted text-uppercase fw-semibold">Succeeded</small>
              </div>
            </div>
            <div class="col-md-4">
              <div class="p-3 border rounded bg-light">
                <div class="h3 mb-0 text-danger">{{ log.failedCount }}</div>
                <small class="text-muted text-uppercase fw-semibold">Failed</small>
              </div>
            </div>
          </div>

          <h6 class="text-secondary mb-3">Item Breakdown</h6>
          <table class="table table-bordered table-hover">
            <thead class="table-light">
              <tr>
                <th>Transaction Name</th>
                <th>From DocType</th>
                <th>To DocType</th>
                <th class="text-center" style="width: 120px;">Status</th>
                <th>Error Details</th>
                <th class="text-center" style="width: 80px;">Retries</th>
                <th class="text-end" style="width: 120px;">Action</th>
              </tr>
            </thead>
            <tbody>
              @for (d of log.details; track d.id) {
                <tr>
                  <td class="fw-semibold">{{ d.transactionName }}</td>
                  <td>{{ d.fromDocType }}</td>
                  <td>{{ d.toDocType }}</td>
                  <td class="text-center">
                    <span class="badge" [ngClass]="getStatusClass(d.status)">
                      {{ getStatusText(d.status) }}
                    </span>
                  </td>
                  <td>
                    @if (d.errorDescription) {
                      <span class="text-danger small font-monospace">{{ d.errorDescription }}</span>
                    } @else {
                      <span class="text-muted">—</span>
                    }
                  </td>
                  <td class="text-center">{{ d.retriedCount }}</td>
                  <td class="text-end">
                    @if (d.status === 3 || d.status === 4) {
                      <button class="btn btn-sm btn-outline-warning" (click)="retry(d)">Retry</button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="7" class="text-center text-muted py-4">No detail records logged.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    }
  `
})
export class BulkTransactionLogDetailComponent implements OnInit {
  private service = inject(BulkTransactionLogService);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  log: BulkTransactionLogDto | null = null;
  id: string | null = null;

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.load();
    }
  }

  load() {
    this.service.get(this.id!).subscribe(res => {
      this.log = res;
    });
  }

  retry(d: BulkTransactionLogDetailDto) {
    this.service.retryDetail(this.id!, d.id!).subscribe({
      next: (res) => {
        this.log = res;
        this.toaster.success('Retry initiated');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Retry failed'),
    });
  }

  getStatusClass(status: BulkTransactionStatus): string {
    switch (status) {
      case BulkTransactionStatus.Success: return 'bg-success';
      case BulkTransactionStatus.Failed: return 'bg-danger';
      case BulkTransactionStatus.InProgress: return 'bg-info';
      case BulkTransactionStatus.Retried: return 'bg-warning text-dark';
      default: return 'bg-secondary';
    }
  }

  getStatusText(status: BulkTransactionStatus): string {
    switch (status) {
      case BulkTransactionStatus.Queued: return 'Queued';
      case BulkTransactionStatus.InProgress: return 'In Progress';
      case BulkTransactionStatus.Success: return 'Success';
      case BulkTransactionStatus.Failed: return 'Failed';
      case BulkTransactionStatus.Retried: return 'Retried';
      default: return 'Unknown';
    }
  }
}
