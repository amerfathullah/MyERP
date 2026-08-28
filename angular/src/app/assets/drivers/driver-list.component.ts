import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { DriverService } from '../../proxy/assets/driver.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { DriverDto } from '../../proxy/assets/models';

const STATUS_LABELS: Record<number, string> = { 0: 'Active', 1: 'Suspended', 2: 'Left' };
const STATUS_BADGE: Record<number, string> = { 0: 'bg-success', 1: 'bg-warning text-dark', 2: 'bg-secondary' };

@Component({
  selector: 'app-driver-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'Drivers' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'Drivers' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/drivers/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewDriver' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-id-badge fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoDriversYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/assets/drivers/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewDriver' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'FullName' | abpLocalization }}</th>
                <th>{{ 'CellNumber' | abpLocalization }}</th>
                <th>{{ 'LicenseNumber' | abpLocalization }}</th>
                <th>{{ 'LicenseExpiryDate' | abpLocalization }}</th>
                <th>{{ 'DriverStatus' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (d of items(); track d.id) {
                  <tr>
                    <td class="fw-semibold">{{ d.fullName }}</td>
                    <td>{{ d.cellNumber || '—' }}</td>
                    <td>{{ d.licenseNumber }}</td>
                    <td>{{ d.licenseExpiryDate ? (d.licenseExpiryDate | date:'dd/MM/yyyy') : '—' }}</td>
                    <td><span class="badge" [class]="STATUS_BADGE[d.status ?? 0]">{{ STATUS_LABELS[d.status ?? 0] }}</span></td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (d.status === 0) {
                          <button class="btn btn-outline-warning" title="Suspend" (click)="suspend(d)"><i class="fa fa-pause"></i></button>
                        }
                        @if (d.status === 1) {
                          <button class="btn btn-outline-success" title="Reinstate" (click)="reinstate(d)"><i class="fa fa-play"></i></button>
                        }
                        @if (d.status === 0 || d.status === 1) {
                          <button class="btn btn-outline-secondary" title="Mark Left" (click)="markLeft(d)"><i class="fa fa-right-from-bracket"></i></button>
                        }
                        <a class="btn btn-outline-primary" [routerLink]="['/assets/drivers', d.id, 'edit']">
                          <i class="fa fa-edit"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(d)">
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
export class DriverListComponent implements OnInit {
  private service = inject(DriverService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  STATUS_LABELS = STATUS_LABELS;
  STATUS_BADGE = STATUS_BADGE;

  items = signal<DriverDto[]>([]);
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

  suspend(d: DriverDto): void {
    this.service.suspend(d.id!).subscribe({ next: () => this.loadData() });
  }

  reinstate(d: DriverDto): void {
    this.service.reinstate(d.id!).subscribe({ next: () => this.loadData() });
  }

  markLeft(d: DriverDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.markLeft(d.id!).subscribe({ next: () => this.loadData() });
    });
  }

  delete(d: DriverDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(d.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
