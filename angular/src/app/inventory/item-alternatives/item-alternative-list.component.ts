import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ItemAlternativeStore } from '../store/item-alternative.store';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-item-alternative-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">
          <i class="bi bi-shuffle me-2"></i>{{ 'MyERP::ItemAlternatives' | abpLocalization }}
        </h5>
        <a routerLink="new" class="btn btn-primary btn-sm">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewItemAlternative' | abpLocalization }}
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
                  <th>{{ 'MyERP::ItemCode' | abpLocalization }}</th>
                  <th>{{ 'MyERP::ItemName' | abpLocalization }}</th>
                  <th>{{ 'MyERP::AlternativeItemCode' | abpLocalization }}</th>
                  <th>{{ 'MyERP::AlternativeItemName' | abpLocalization }}</th>
                  <th class="text-center">{{ 'MyERP::TwoWay' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of store.entities(); track item.id) {
                  <tr>
                    <td>
                      <a [routerLink]="[item.id, 'edit']" class="fw-semibold text-decoration-none">
                        {{ item.itemCode || item.itemId }}
                      </a>
                    </td>
                    <td>{{ item.itemName || '-' }}</td>
                    <td>{{ item.alternativeItemCode || item.alternativeItemId }}</td>
                    <td>{{ item.alternativeItemName || '-' }}</td>
                    <td class="text-center">
                      @if (item.twoWay) {
                        <span class="badge bg-success"><i class="bi bi-arrow-left-right me-1"></i>Yes</span>
                      } @else {
                        <span class="badge bg-secondary"><i class="bi bi-arrow-right me-1"></i>One-way</span>
                      }
                    </td>
                    <td class="text-end">
                      <div class="btn-group btn-group-sm">
                        <a [routerLink]="[item.id, 'edit']" class="btn btn-outline-primary" title="Edit">
                          <i class="bi bi-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(item.id)" title="Delete">
                          <i class="bi bi-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="6" class="text-center text-muted py-4">
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
export class ItemAlternativeListComponent implements OnInit {
  protected readonly store = inject(ItemAlternativeStore);
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
