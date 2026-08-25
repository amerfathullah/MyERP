import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { EmployeeGroupService } from '../../proxy/human-resources/employee-group.service';
import { EmployeeGroupDto } from '../../proxy/human-resources/models';

@Component({
  selector: 'app-employee-group-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Employee Groups</h5>
        <a routerLink="/hr/employee-groups/new" class="btn btn-primary btn-sm">New Employee Group</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Group Name</th>
              <th>Member Count</th>
              <th>Status</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/hr/employee-groups', item.id, 'edit']" class="fw-semibold">
                    {{ item.groupName }}
                  </a>
                </td>
                <td>
                  <span class="badge bg-secondary">{{ item.items?.length || 0 }} members</span>
                </td>
                <td>
                  @if (item.isDisabled) {
                    <span class="badge bg-warning text-dark">Disabled</span>
                  } @else {
                    <span class="badge bg-success">Active</span>
                  }
                </td>
                <td class="text-end">
                  <a [routerLink]="['/hr/employee-groups', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="text-center text-muted py-4">No employee groups configured yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class EmployeeGroupListComponent implements OnInit {
  private service = inject(EmployeeGroupService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: EmployeeGroupDto[] = [];

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: EmployeeGroupDto): void {
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
