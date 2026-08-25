import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { TerritoryService } from '../../proxy/core/territory.service';
import { TerritoryDto } from '../../proxy/core/models';

@Component({
  selector: 'app-territory-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Territories</h5>
        <a routerLink="/settings/territories/new" class="btn btn-primary btn-sm">New Territory</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Name</th>
              <th>Type</th>
              <th>Parent Territory</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/settings/territories', item.id, 'edit']" class="fw-semibold">
                    {{ item.name }}
                  </a>
                </td>
                <td>
                  @if (item.isGroup) {
                    <span class="badge bg-primary">Group (Region)</span>
                  } @else {
                    <span class="badge bg-secondary">Leaf Territory</span>
                  }
                </td>
                <td>{{ item.parentName || '—' }}</td>
                <td class="text-end">
                  <a [routerLink]="['/settings/territories', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="text-center text-muted py-4">No territories configured yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class TerritoryListComponent implements OnInit {
  private service = inject(TerritoryService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: TerritoryDto[] = [];

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: TerritoryDto): void {
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
