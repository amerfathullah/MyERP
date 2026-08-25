import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { ShipmentParcelTemplateService } from '../../proxy/inventory/shipment-parcel-template.service';
import { ShipmentParcelTemplateDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-shipment-parcel-template-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Shipment Parcel Templates</h5>
        <a routerLink="/inventory/shipment-parcel-templates/new" class="btn btn-primary btn-sm">New Template</a>
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
              <th>Template Name</th>
              <th class="text-end">Length (cm)</th>
              <th class="text-end">Width (cm)</th>
              <th class="text-end">Height (cm)</th>
              <th class="text-end">Weight (kg)</th>
              <th>Description</th>
              <th class="text-center">Status</th>
              <th class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/inventory/shipment-parcel-templates', item.id, 'edit']" class="fw-semibold">
                    {{ item.parcelTemplateName }}
                  </a>
                </td>
                <td class="text-end">{{ item.length | number:'1.2-2' }}</td>
                <td class="text-end">{{ item.width | number:'1.2-2' }}</td>
                <td class="text-end">{{ item.height | number:'1.2-2' }}</td>
                <td class="text-end">{{ item.weight | number:'1.2-2' }}</td>
                <td>{{ item.description || '—' }}</td>
                <td class="text-center">
                  <span class="badge" [ngClass]="item.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ item.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td class="text-end">
                  <a [routerLink]="['/inventory/shipment-parcel-templates', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">Edit</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">Delete</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="8" class="text-center text-muted py-4">No shipment parcel templates found.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ShipmentParcelTemplateListComponent implements OnInit {
  private service = inject(ShipmentParcelTemplateService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: ShipmentParcelTemplateDto[] = [];
  filter = '';

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ filter: this.filter, maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: ShipmentParcelTemplateDto): void {
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
