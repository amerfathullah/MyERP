import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { LocationService } from '../../proxy/assets/location.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import type { LocationDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-location-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'Locations' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'Locations' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/locations/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewLocation' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-map-marker-alt fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoLocationsYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/assets/locations/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewLocation' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'LocationName' | abpLocalization }}</th>
                <th>{{ 'ParentLocation' | abpLocalization }}</th>
                <th>{{ 'IsGroup' | abpLocalization }}</th>
                <th>{{ 'IsContainer' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (loc of items(); track loc.id) {
                  <tr>
                    <td class="fw-semibold">{{ loc.locationName }}</td>
                    <td>{{ loc.parentLocationName ?? '—' }}</td>
                    <td>
                      @if (loc.isGroup) {
                        <span class="badge bg-secondary">{{ 'Yes' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-light text-dark">{{ 'No' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      @if (loc.isContainer) {
                        <span class="badge bg-secondary">{{ 'Yes' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-light text-dark">{{ 'No' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/assets/locations', loc.id, 'edit']">
                          <i class="fa fa-edit"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(loc)">
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
export class LocationListComponent implements OnInit {
  private service = inject(LocationService);
  private confirmation = inject(ConfirmationService);

  items = signal<LocationDto[]>([]);
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

  delete(loc: LocationDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(loc.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
