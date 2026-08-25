import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { OpportunityLostReasonService } from '../../proxy/crm/opportunity-lost-reason.service';
import { OpportunityLostReasonDto } from '../../proxy/crm/models';

@Component({
  selector: 'app-opportunity-lost-reason-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Opportunity Lost Reasons</h5>
        <a routerLink="/crm/opportunity-lost-reasons/new" class="btn btn-primary btn-sm">New Lost Reason</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>Reason</th>
              <th>Description</th>
              <th>Status</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/crm/opportunity-lost-reasons', item.id, 'edit']" class="fw-semibold">
                    {{ item.reason }}
                  </a>
                </td>
                <td>{{ item.description || '—' }}</td>
                <td>
                  @if (item.isDisabled) {
                    <span class="badge bg-warning text-dark">Disabled</span>
                  } @else {
                    <span class="badge bg-success">Active</span>
                  }
                </td>
                <td class="text-end">
                  <a [routerLink]="['/crm/opportunity-lost-reasons', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="text-center text-muted py-4">No opportunity lost reasons configured yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class OpportunityLostReasonListComponent implements OnInit {
  private service = inject(OpportunityLostReasonService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: OpportunityLostReasonDto[] = [];

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: OpportunityLostReasonDto): void {
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
