import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { PartyTypeService } from '../../proxy/core/party-type.service';
import { PartyTypeDto } from '../../proxy/core/models';

@Component({
  selector: 'app-party-type-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Party Types</h5>
        <a routerLink="/settings/party-types/new" class="btn btn-primary btn-sm">New Party Type</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by name..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Party Type</th>
              <th>Account Type</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/settings/party-types', item.id, 'edit']" class="fw-semibold">
                    {{ item.name }}
                  </a>
                </td>
                <td>
                  <span class="badge" [class.bg-success]="item.accountType === 1" [class.bg-warning]="item.accountType === 0">
                    {{ item.accountType === 1 ? 'Receivable' : 'Payable' }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/settings/party-types', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="3" class="text-center text-muted py-4">No party types configured yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class PartyTypeListComponent implements OnInit {
  private service = inject(PartyTypeService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: PartyTypeDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: PartyTypeDto): void {
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
