import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { ActivityCostService } from '../../proxy/projects/activity-cost.service';
import { ActivityCostDto } from '../../proxy/projects/models';

@Component({
  selector: 'app-activity-cost-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Activity Costs</h5>
        <a routerLink="/projects/activity-costs/new" class="btn btn-primary btn-sm">New Activity Cost</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Employee</th>
              <th>Department</th>
              <th>Activity Type</th>
              <th class="text-end">Billing Rate (/hr)</th>
              <th class="text-end">Costing Rate (/hr)</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/projects/activity-costs', item.id, 'edit']" class="fw-semibold">
                    {{ item.employeeName || item.employeeId }}
                  </a>
                </td>
                <td>{{ item.department || '—' }}</td>
                <td>{{ item.activityTypeName || item.activityTypeId }}</td>
                <td class="text-end">{{ item.billingRate | number:'1.2-2' }}</td>
                <td class="text-end">{{ item.costingRate | number:'1.2-2' }}</td>
                <td class="text-end">
                  <a [routerLink]="['/projects/activity-costs', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6" class="text-center text-muted py-4">No activity costs configured yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ActivityCostListComponent implements OnInit {
  private service = inject(ActivityCostService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: ActivityCostDto[] = [];

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: ActivityCostDto): void {
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
