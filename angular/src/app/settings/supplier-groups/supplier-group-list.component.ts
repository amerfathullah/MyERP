import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { SupplierGroupService } from '../../proxy/core/supplier-group.service';
import { SupplierGroupDto } from '../../proxy/core/models';

@Component({
  selector: 'app-supplier-group-list',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::SupplierGroups' | abpLocalization }}</h5>
        <a routerLink="/settings/supplier-groups/new" class="btn btn-primary btn-sm">{{ '::NewSupplierGroup' | abpLocalization }}</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>{{ '::Name' | abpLocalization }}</th>
              <th>{{ '::Type' | abpLocalization }}</th>
              <th>{{ '::ParentGroup' | abpLocalization }}</th>
              <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/settings/supplier-groups', item.id, 'edit']" class="fw-semibold">
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
                <td class="text-end">
                  <a [routerLink]="['/settings/supplier-groups', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">{{ '::Edit' | abpLocalization }}</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">{{ '::Delete' | abpLocalization }}</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="text-center text-muted py-4">{{ '::NoSupplierGroupsConfigured' | abpLocalization }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class SupplierGroupListComponent implements OnInit {
  private service = inject(SupplierGroupService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: SupplierGroupDto[] = [];

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: SupplierGroupDto): void {
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
