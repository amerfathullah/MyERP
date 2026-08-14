import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ManufacturerStore } from '../store/manufacturer.store';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-manufacturer-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">
          <i class="bi bi-building-gear me-2"></i>{{ 'MyERP::Manufacturers' | abpLocalization }}
        </h5>
        <a routerLink="new" class="btn btn-primary btn-sm">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewManufacturer' | abpLocalization }}
        </a>
      </div>
      <div class="card-body">
        @if (store.isLoading()) {
          <div class="text-center py-4">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'MyERP::ShortName' | abpLocalization }}</th>
                  <th>{{ 'MyERP::FullName' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Country' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Website' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (mfr of store.entities(); track mfr.id) {
                  <tr>
                    <td>
                      <a [routerLink]="[mfr.id, 'edit']" class="fw-semibold text-decoration-none">
                        {{ mfr.shortName }}
                      </a>
                    </td>
                    <td>{{ mfr.fullName || '-' }}</td>
                    <td>{{ mfr.country || '-' }}</td>
                    <td>
                      @if (mfr.website) {
                        <a [href]="mfr.website" target="_blank" class="text-decoration-none">
                          <i class="bi bi-globe me-1"></i>{{ mfr.website }}
                        </a>
                      } @else {
                        -
                      }
                    </td>
                    <td class="text-end">
                      <div class="btn-group btn-group-sm">
                        <a [routerLink]="[mfr.id, 'edit']" class="btn btn-outline-primary" title="Edit">
                          <i class="bi bi-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(mfr.id)" title="Delete">
                          <i class="bi bi-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="text-center text-muted py-4">
                      {{ 'MyERP::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <app-pagination
            [totalCount]="store.totalCount()"
            [pageSize]="pageSize"
            [currentPage]="pageIndex"
            (pageChange)="onPageChange($event)"
          />
        }
      </div>
    </div>
  `
})
export class ManufacturerListComponent implements OnInit {
  protected readonly store = inject(ManufacturerStore);
  private readonly confirmation = inject(ConfirmationService);

  pageIndex = 0;
  pageSize = 10;

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.store.load({
      skipCount: this.pageIndex * this.pageSize,
      maxResultCount: this.pageSize,
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  delete(id: string): void {
    this.confirmation.warn('MyERP::DeleteConfirmationMessage', 'MyERP::Delete').subscribe((status: Confirmation.Status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.delete(id);
      }
    });
  }
}
