import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductBundleService } from '../../proxy/sales/product-bundle.service';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { Confirmation, ToasterService , ConfirmationService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-product-bundle-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe, PaginationComponent],
  template: `
    <div class="container-fluid py-3">
      <div class="card shadow-sm">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-cubes me-2"></i>{{ '::ProductBundles' | abpLocalization }}</h5>
          <a routerLink="/sales/product-bundles/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ '::NewProductBundle' | abpLocalization }}
          </a>
        </div>

        <div class="card-body p-0">
          @if (items().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-cubes fa-2x mb-2"></i>
              <p>{{ '::NoProductBundlesYet' | abpLocalization }}</p>
              <a routerLink="/sales/product-bundles/new" class="btn btn-outline-primary btn-sm">
                {{ '::CreateProductBundle' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::ParentItem' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Components' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (bundle of items(); track bundle.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/sales/product-bundles', bundle.id]">
                        {{ bundle.parentItemName || '—' }}
                      </a>
                    </td>
                    <td class="text-end">{{ bundle.items?.length || 0 }}</td>
                    <td>
                      @if (bundle.isActive) {
                        <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-secondary">{{ '::Inactive' | abpLocalization }}</span>
                      }
                    </td>
                    <td class="text-end">
                      @if (bundle.isActive) {
                        <button class="btn btn-outline-warning btn-sm" (click)="deactivate(bundle.id)">
                          <i class="fa fa-toggle-off me-1"></i>{{ '::Deactivate' | abpLocalization }}
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
        @if (totalCount() > 20) {
          <div class="card-footer">
            <app-pagination [totalCount]="totalCount()" [pageSize]="20" [currentPage]="currentPage"
              (pageChange)="onPageChange($event)" />
          </div>
        }
      </div>
    </div>
  `,
})
export class ProductBundleListComponent implements OnInit {
  private service = inject(ProductBundleService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<any[]>([]);
  totalCount = signal(0);
  currentPage = 0;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.service.getList({
      skipCount: this.currentPage * 20,
      maxResultCount: 20,
    } as any).subscribe({
      next: (res) => {
        this.items.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
      },
      error: () => {},
    });
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  deactivate(id: string) {
    this.confirmation.warn('::DeactivateConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.deactivate(id).subscribe({
        next: () => { this.toaster.success('::Deactivated'); this.loadData(); },
        error: () => {},
      });
    });
  }
}
