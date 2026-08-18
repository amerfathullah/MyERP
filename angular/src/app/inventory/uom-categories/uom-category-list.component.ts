import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { UomCategoryService } from '../../proxy/inventory/uom-category.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import type { UomCategoryDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-uom-category-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'UomCategories' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'UomCategories' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/inventory/uom-categories/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewUomCategory' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-ruler fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoUomCategoriesYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/inventory/uom-categories/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewUomCategory' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'Name' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (c of items(); track c.id) {
                  <tr>
                    <td class="fw-semibold">{{ c.name }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/inventory/uom-categories', c.id, 'edit']">
                          <i class="fa fa-edit"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(c)">
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
export class UomCategoryListComponent implements OnInit {
  private service = inject(UomCategoryService);
  private confirmation = inject(ConfirmationService);

  items = signal<UomCategoryDto[]>([]);
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

  delete(c: UomCategoryDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(c.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
