import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { BulkTransactionLogService } from '../../proxy/automation/bulk-transaction-log.service';
import { BulkTransactionLogDto } from '../../proxy/automation/models';

@Component({
  selector: 'app-bulk-transaction-log-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Bulk Transaction Logs</h5>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by title..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Title</th>
              <th>Batch Date</th>
              <th class="text-center" style="width: 120px;">Total</th>
              <th class="text-center" style="width: 120px;">Succeeded</th>
              <th class="text-center" style="width: 120px;">Failed</th>
              <th class="text-end" style="width: 140px;">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/automation/bulk-transactions', item.id]" class="fw-semibold">
                    {{ item.title }}
                  </a>
                </td>
                <td>{{ item.batchDate | date:'medium' }}</td>
                <td class="text-center">
                  <span class="badge bg-secondary">{{ item.totalEntries }}</span>
                </td>
                <td class="text-center">
                  <span class="badge bg-success">{{ item.succeededCount }}</span>
                </td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.failedCount > 0 ? 'bg-danger' : 'bg-light text-muted'">
                    {{ item.failedCount }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/automation/bulk-transactions', item.id]" class="btn btn-sm btn-outline-primary me-2">View</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6" class="text-center text-muted py-4">No bulk transaction logs found.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class BulkTransactionLogListComponent implements OnInit {
  private service = inject(BulkTransactionLogService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: BulkTransactionLogDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: BulkTransactionLogDto): void {
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
