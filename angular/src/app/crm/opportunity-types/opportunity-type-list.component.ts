import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { OpportunityTypeService } from '../../proxy/crm/opportunity-type.service';
import { OpportunityTypeDto } from '../../proxy/crm/models';

@Component({
  selector: 'app-opportunity-type-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Opportunity Types</h5>
        <a routerLink="/crm/opportunity-types/new" class="btn btn-primary btn-sm">New Opportunity Type</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by type name..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Type Name</th>
              <th>Description</th>
              <th class="text-center" style="width: 100px;">Status</th>
              <th class="text-end" style="width: 160px;">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/crm/opportunity-types', item.id, 'edit']" class="fw-semibold">
                    {{ item.name }}
                  </a>
                </td>
                <td>{{ item.description || '—' }}</td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ item.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/crm/opportunity-types', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="text-center text-muted py-4">No opportunity types found.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class OpportunityTypeListComponent implements OnInit {
  private service = inject(OpportunityTypeService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: OpportunityTypeDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: OpportunityTypeDto): void {
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
