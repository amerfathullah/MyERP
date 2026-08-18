import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { SalesForecastService } from '../../proxy/manufacturing/sales-forecast.service';
import type { SalesForecastDto } from '../../proxy/manufacturing/models';

/**
 * Sales Forecast list — demand-planning masters (item x warehouse x period) that feed
 * Master Production Schedule via "Generate Demand" / "Create MPS".
 * Per ERPNext: Sales Forecast (manufacturing/doctype/sales_forecast).
 */
@Component({
  selector: 'app-sales-forecast-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'SalesForecasts' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/manufacturing/sales-forecasts/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewSalesForecast' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) {
        <app-loading-overlay />
      }

      @if (!isLoading && forecasts.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-chart-line fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSalesForecastsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'ForecastNumber' | abpLocalization }}</th>
                  <th>{{ 'PostingDate' | abpLocalization }}</th>
                  <th>{{ 'FromDate' | abpLocalization }}</th>
                  <th>{{ 'Frequency' | abpLocalization }}</th>
                  <th class="text-end">{{ 'DemandRows' | abpLocalization }}</th>
                  <th>{{ 'ForecastStatus' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (f of forecasts; track f.id) {
                  <tr>
                    <td>{{ f.forecastNumber }}</td>
                    <td>{{ f.postingDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ f.fromDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ f.frequency }}</td>
                    <td class="text-end">{{ f.items?.length ?? 0 }}</td>
                    <td>{{ f.forecastStatus }}</td>
                    <td><app-status-badge [status]="statusLabel(f.status)"></app-status-badge></td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/manufacturing/sales-forecasts', f.id]">
                          <i class="fa fa-eye"></i>
                        </a>
                        @if ((f.status ?? 0) === 0) {
                          <button class="btn btn-outline-danger" (click)="remove(f)"><i class="fa fa-trash"></i></button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <app-pagination
        [totalCount]="totalCount"
        [pageSize]="pageSize"
        [currentPage]="currentPage"
        (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class SalesForecastListComponent implements OnInit {
  private service = inject(SalesForecastService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  forecasts: SalesForecastDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.forecasts = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  statusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', '', '', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  remove(f: SalesForecastDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(f.id!).subscribe(() => {
        this.toaster.success('::SuccessfullyDeleted');
        this.load();
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
