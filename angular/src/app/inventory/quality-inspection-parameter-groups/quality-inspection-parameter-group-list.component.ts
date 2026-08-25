import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { QualityInspectionParameterGroupService } from '../../proxy/inventory/quality-inspection-parameter-group.service';
import { QualityInspectionParameterGroupDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-quality-inspection-parameter-group-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Quality Inspection Parameter Groups</h5>
        <a routerLink="/inventory/quality-inspection-parameter-groups/new" class="btn btn-primary btn-sm">New Group</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by group name..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Group Name</th>
              <th>Description</th>
              <th class="text-center">Status</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/inventory/quality-inspection-parameter-groups', item.id, 'edit']" class="fw-semibold">
                    {{ item.groupName }}
                  </a>
                </td>
                <td>{{ item.description || '—' }}</td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ item.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/inventory/quality-inspection-parameter-groups', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="text-center text-muted py-4">No parameter groups found.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class QualityInspectionParameterGroupListComponent implements OnInit {
  private service = inject(QualityInspectionParameterGroupService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: QualityInspectionParameterGroupDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: QualityInspectionParameterGroupDto): void {
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
