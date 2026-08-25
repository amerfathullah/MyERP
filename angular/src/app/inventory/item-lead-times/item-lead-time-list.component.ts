import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { ItemLeadTimeService } from '../../proxy/inventory/item-lead-time.service';
import { ItemLeadTimeDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-item-lead-time-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Item Lead Times</h5>
        <a routerLink="/inventory/item-lead-times/new" class="btn btn-primary btn-sm">New Lead Time</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by item..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Item</th>
              <th class="text-end">Shift (Hrs)</th>
              <th class="text-end">Workstations</th>
              <th class="text-end">Mfg Time (Mins)</th>
              <th class="text-end">Daily Yield</th>
              <th class="text-end">Capacity / Day</th>
              <th class="text-end">Purchase (Days)</th>
              <th class="text-end">Buffer (Days)</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/inventory/item-lead-times', item.id, 'edit']" class="fw-semibold">
                    {{ item.itemCode }} - {{ item.itemName }}
                  </a>
                </td>
                <td class="text-end">{{ item.shiftTimeInHours }}</td>
                <td class="text-end">{{ item.noOfWorkstations }}</td>
                <td class="text-end">{{ item.manufacturingTimeInMins }}</td>
                <td class="text-end">{{ item.dailyYield }}%</td>
                <td class="text-end fw-semibold text-primary">{{ item.capacityPerDay }}</td>
                <td class="text-end">{{ item.purchaseTimeDays }}</td>
                <td class="text-end">{{ item.bufferTimeDays }}</td>
                <td class="text-end">
                  <a [routerLink]="['/inventory/item-lead-times', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="9" class="text-center text-muted py-4">No item lead time profiles configured.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ItemLeadTimeListComponent implements OnInit {
  private service = inject(ItemLeadTimeService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: ItemLeadTimeDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: ItemLeadTimeDto): void {
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
