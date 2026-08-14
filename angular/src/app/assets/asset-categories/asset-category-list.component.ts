import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { AssetCategoryService } from '../../proxy/assets/asset-category.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import type { AssetCategoryDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-category-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'AssetCategories' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetCategories' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/categories/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewAssetCategory' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-tags fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoAssetCategoriesYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/assets/categories/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewAssetCategory' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'CategoryName' | abpLocalization }}</th>
                <th>{{ 'Depreciable' | abpLocalization }}</th>
                <th>{{ 'DepreciationMethod' | abpLocalization }}</th>
                <th>{{ 'UsefulLifeMonths' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (cat of items(); track cat.id) {
                  <tr>
                    <td class="fw-semibold">{{ cat.categoryName }}</td>
                    <td>
                      @if (cat.isDepreciable) {
                        <span class="badge bg-success">Yes</span>
                      } @else {
                        <span class="badge bg-secondary">No</span>
                      }
                    </td>
                    <td>{{ cat.defaultDepreciationMethod ?? '—' }}</td>
                    <td>{{ cat.defaultUsefulLifeMonths ?? '—' }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/assets/categories', cat.id, 'edit']">
                          <i class="fa fa-edit"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(cat)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize"
        [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class AssetCategoryListComponent implements OnInit {
  private service = inject(AssetCategoryService);
  private confirmation = inject(ConfirmationService);

  items = signal<AssetCategoryDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize } as any).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  delete(cat: AssetCategoryDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(cat.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
