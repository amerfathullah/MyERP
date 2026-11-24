import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomerGroupService } from '../../proxy/core/customer-group.service';
import { CustomerGroupDto } from '../../proxy/core/models';

@Component({
  selector: 'app-customer-group-list',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::CustomerGroups' | abpLocalization }}</h5>
        <a routerLink="/settings/customer-groups/new" class="btn btn-primary btn-sm">{{ '::NewCustomerGroup' | abpLocalization }}</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>{{ '::Name' | abpLocalization }}</th>
              <th>{{ '::Type' | abpLocalization }}</th>
              <th>{{ '::ParentGroup' | abpLocalization }}</th>
              <th>{{ '::DefaultCreditLimit' | abpLocalization }}</th>
              <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/settings/customer-groups', item.id, 'edit']" class="fw-semibold">
                    {{ item.name }}
                  </a>
                </td>
                <td>
                  @if (item.isGroup) {
                    <span class="badge bg-primary">{{ '::GroupFolder' | abpLocalization }}</span>
                  } @else {
                    <span class="badge bg-secondary">{{ '::Leaf' | abpLocalization }}</span>
                  }
                </td>
                <td>{{ item.parentName || '—' }}</td>
                <td>{{ item.defaultCreditLimit | number:'1.2-2' }}</td>
                <td class="text-end">
                  <a [routerLink]="['/settings/customer-groups', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">{{ '::Edit' | abpLocalization }}</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">{{ '::Delete' | abpLocalization }}</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="text-center text-muted py-4">{{ '::NoCustomerGroupsConfigured' | abpLocalization }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class CustomerGroupListComponent implements OnInit {
  private service = inject(CustomerGroupService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: CustomerGroupDto[] = [];

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: CustomerGroupDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(item.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          this.load();
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::Failed'),
      });
    });
  }
}
