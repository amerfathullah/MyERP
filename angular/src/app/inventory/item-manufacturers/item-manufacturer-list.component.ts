import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ItemManufacturerService } from '../../proxy/inventory/item-manufacturer.service';
import type { ItemManufacturerDto } from '../../proxy/inventory/models';
import { CompanyContextService } from '../../shared/services/company-context.service';

export function filterItemManufacturers(
  items: ItemManufacturerDto[],
  searchTerm: string,
  companyId: string,
): ItemManufacturerDto[] {
  let result = items;
  if (companyId) {
    result = result.filter(i => !i.companyId || i.companyId === companyId);
  }
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return result;
  }
  return result.filter(i =>
    [
      i.itemCode,
      i.itemName,
      i.manufacturerShortName,
      i.manufacturerPartNo,
      i.description,
    ].some(v => (v ?? '').toLowerCase().includes(term)),
  );
}

@Component({
  selector: 'app-item-manufacturer-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-cogs text-primary me-2"></i>
            {{ '::ItemManufacturers' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::ItemManufacturersDescription' | abpLocalization }}
          </p>
        </div>
        <a routerLink="/inventory/item-manufacturers/new" class="btn btn-primary btn-sm">
          <i class="fas fa-plus me-1"></i>{{ '::NewItemManufacturer' | abpLocalization }}
        </a>
      </div>

      <div class="card mb-3 border-0 shadow-sm">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-5">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'::ManufacturerPartNo' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
              />
            </div>
            <div class="col-md-2">
              <button class="btn btn-sm btn-outline-primary" (click)="load()" [disabled]="loading()">
                <i class="fas fa-sync me-1"></i>{{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="card border-0 shadow-sm">
        <div class="table-responsive">
          <table class="table table-hover table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>{{ '::ItemCode' | abpLocalization }}</th>
                <th>{{ '::ItemName' | abpLocalization }}</th>
                <th>{{ '::Manufacturers' | abpLocalization }}</th>
                <th>{{ '::ManufacturerPartNo' | abpLocalization }}</th>
                <th>{{ '::Default' | abpLocalization }}</th>
                <th>{{ '::Description' | abpLocalization }}</th>
                <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (loading()) {
                <tr>
                  <td colspan="7" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}
                  </td>
                </tr>
              } @else if (filteredItems().length === 0) {
                <tr>
                  <td colspan="7" class="text-center py-4 text-muted">
                    {{ '::NoDataAvailable' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredItems(); track row.id) {
                  <tr>
                    <td class="fw-semibold">{{ row.itemCode || '-' }}</td>
                    <td>{{ row.itemName || '-' }}</td>
                    <td>{{ row.manufacturerShortName || '-' }}</td>
                    <td class="font-monospace">{{ row.manufacturerPartNo }}</td>
                    <td>
                      @if (row.isDefault) {
                        <span class="badge bg-success">{{ '::Default' | abpLocalization }}</span>
                      }
                    </td>
                    <td class="text-muted small">{{ row.description || '-' }}</td>
                    <td class="text-end">
                      <a
                        [routerLink]="['/inventory/item-manufacturers', row.id, 'edit']"
                        class="btn btn-sm btn-outline-primary me-1"
                      >
                        <i class="fas fa-edit"></i>
                      </a>
                      <button class="btn btn-sm btn-outline-danger" (click)="remove(row)">
                        <i class="fas fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class ItemManufacturerListComponent implements OnInit {
  private service = inject(ItemManufacturerService);
  private companyContext = inject(CompanyContextService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<ItemManufacturerDto[]>([]);
  loading = signal(false);
  searchTerm = signal('');

  filteredItems = computed(() =>
    filterItemManufacturers(
      this.items(),
      this.searchTerm(),
      this.companyContext.currentCompanyId(),
    ),
  );

  ngOnInit(): void {
    this.companyContext.load();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: res => {
        this.items.set(res.items ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  remove(row: ItemManufacturerDto): void {
    if (!row.id) return;
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(row.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          this.load();
        },
      });
    });
  }
}
