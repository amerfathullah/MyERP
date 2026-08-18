import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { VehicleService } from '../../proxy/assets/vehicle.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { VehicleDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-vehicle-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'Vehicles' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'Vehicles' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/vehicles/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewVehicle' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-truck fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoVehiclesYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/assets/vehicles/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewVehicle' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'LicensePlate' | abpLocalization }}</th>
                <th>{{ 'Make' | abpLocalization }}</th>
                <th>{{ 'Model' | abpLocalization }}</th>
                <th>{{ 'AssignedDriver' | abpLocalization }}</th>
                <th>{{ 'LastOdometer' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (v of items(); track v.id) {
                  <tr>
                    <td class="fw-semibold">{{ v.licensePlate }}</td>
                    <td>{{ v.make || '—' }}</td>
                    <td>{{ v.model || '—' }}</td>
                    <td>{{ v.driverName || '—' }}</td>
                    <td>{{ v.lastOdometer }}</td>
                    <td>
                      @if (v.isDisabled) {
                        <span class="badge bg-secondary">{{ 'Disabled' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-success">{{ 'Active' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (!v.isDisabled) {
                          <button class="btn btn-outline-warning" title="Disable" (click)="disable(v)"><i class="fa fa-ban"></i></button>
                        } @else {
                          <button class="btn btn-outline-success" title="Enable" (click)="enable(v)"><i class="fa fa-check"></i></button>
                        }
                        <a class="btn btn-outline-primary" [routerLink]="['/assets/vehicles', v.id, 'edit']">
                          <i class="fa fa-edit"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(v)">
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
export class VehicleListComponent implements OnInit {
  private service = inject(VehicleService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  items = signal<VehicleDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.getList({
      companyId: this.companyContext.currentCompanyId(),
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
    } as any).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  disable(v: VehicleDto): void {
    this.service.disable(v.id!).subscribe({ next: () => this.loadData() });
  }

  enable(v: VehicleDto): void {
    this.service.enable(v.id!).subscribe({ next: () => this.loadData() });
  }

  delete(v: VehicleDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(v.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
