import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { DeliveryTripStore } from '../store/delivery-trip.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-delivery-trip-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, StatusBadgeComponent, PaginationComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">
          <i class="bi bi-truck me-2"></i>{{ 'MyERP::DeliveryTrips' | abpLocalization }}
        </h5>
        <a routerLink="new" class="btn btn-primary btn-sm">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewDeliveryTrip' | abpLocalization }}
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
                  <th>{{ 'MyERP::TripNumber' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Driver' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Vehicle' | abpLocalization }}</th>
                  <th>{{ 'MyERP::DepartureTime' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::TotalDistance' | abpLocalization }}</th>
                  <th class="text-center">{{ 'MyERP::Stops' | abpLocalization }}</th>
                  <th class="text-center">{{ 'MyERP::Status' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (trip of store.entities(); track trip.id) {
                  <tr>
                    <td>
                      <a [routerLink]="[trip.id, 'edit']" class="fw-semibold text-decoration-none">
                        {{ trip.tripNumber }}
                      </a>
                    </td>
                    <td>{{ trip.driverName || trip.driver }}</td>
                    <td>{{ trip.vehicle }}</td>
                    <td>{{ trip.departureTime | date:'short' }}</td>
                    <td class="text-end fw-medium">{{ trip.totalDistance | number:'1.2-2' }} {{ trip.uom || 'Km' }}</td>
                    <td class="text-center">
                      <span class="badge bg-secondary">{{ trip.deliveryStops?.length || 0 }}</span>
                    </td>
                    <td class="text-center">
                      <app-status-badge [status]="getStatusString(trip.status)" />
                    </td>
                    <td class="text-end">
                      <div class="btn-group btn-group-sm">
                        @if (trip.status === 0) {
                          <button class="btn btn-outline-info" (click)="schedule(trip.id)" title="Schedule">
                            <i class="bi bi-calendar-check"></i>
                          </button>
                        }
                        @if (trip.status === 1) {
                          <button class="btn btn-outline-primary" (click)="startTransit(trip.id)" title="Start Transit">
                            <i class="bi bi-truck"></i>
                          </button>
                        }
                        @if (trip.status === 2 || trip.status === 1) {
                          <button class="btn btn-outline-success" (click)="complete(trip.id)" title="Complete">
                            <i class="bi bi-check-circle"></i>
                          </button>
                        }
                        @if (trip.status !== 3 && trip.status !== 4) {
                          <button class="btn btn-outline-warning" (click)="cancel(trip.id)" title="Cancel">
                            <i class="bi bi-x-circle"></i>
                          </button>
                        }
                        <a [routerLink]="[trip.id, 'edit']" class="btn btn-outline-primary" title="Edit">
                          <i class="bi bi-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(trip.id)" title="Delete">
                          <i class="bi bi-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="8" class="text-center text-muted py-4">
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
export class DeliveryTripListComponent implements OnInit {
  protected readonly store = inject(DeliveryTripStore);
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

  schedule(id: string): void {
    this.store.schedule(id);
  }

  startTransit(id: string): void {
    this.store.startTransit(id);
  }

  complete(id: string): void {
    this.confirmation.warn('MyERP::CompleteConfirmationMessage', 'MyERP::Complete').subscribe((status: Confirmation.Status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.complete(id);
      }
    });
  }

  cancel(id: string): void {
    this.confirmation.warn('MyERP::CancelConfirmationMessage', 'MyERP::Cancel').subscribe((status: Confirmation.Status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.cancel(id);
      }
    });
  }

  delete(id: string): void {
    this.confirmation.warn('MyERP::DeleteConfirmationMessage', 'MyERP::Delete').subscribe((status: Confirmation.Status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.delete(id);
      }
    });
  }

  getStatusString(status: number): string {
    switch (status) {
      case 0: return 'Draft';
      case 1: return 'Submitted';
      case 2: return 'ToDeliver';
      case 3: return 'Completed';
      case 4: return 'Cancelled';
      default: return 'Draft';
    }
  }
}
