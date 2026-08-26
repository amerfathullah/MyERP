import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { SupplierScorecardVariableService } from '../../proxy/purchasing/supplier-scorecard-variable.service';
import { SupplierScorecardVariableDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-scorecard-variable-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Supplier Scorecard Variables</h5>
        <a routerLink="/purchasing/scorecard-variables/new" class="btn btn-primary btn-sm">New Variable</a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Filter by label or param..." [(ngModel)]="filter" (ngModelChange)="load()">
          </div>
        </div>

        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Variable Label</th>
              <th>Parameter Name</th>
              <th>Path</th>
              <th>Custom</th>
              <th>Description</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/purchasing/scorecard-variables', item.id, 'edit']" class="fw-semibold">
                    {{ item.variableLabel }}
                  </a>
                </td>
                <td><code>{{ item.paramName }}</code></td>
                <td><code>{{ item.path }}</code></td>
                <td>
                  @if (item.isCustom) {
                    <span class="badge bg-info">Custom</span>
                  } @else {
                    <span class="badge bg-secondary">System</span>
                  }
                </td>
                <td>{{ item.description || '—' }}</td>
                <td class="text-end">
                  <a [routerLink]="['/purchasing/scorecard-variables', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6" class="text-center text-muted py-4">No scorecard variables configured yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ScorecardVariableListComponent implements OnInit {
  private service = inject(SupplierScorecardVariableService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: SupplierScorecardVariableDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: SupplierScorecardVariableDto): void {
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
