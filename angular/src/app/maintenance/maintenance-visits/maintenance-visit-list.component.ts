import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import type { MaintenanceVisitDto } from '../../proxy/assets/models';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-maintenance-visit-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent, StatusBadgeComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">{{ 'MyERP::MaintenanceVisits' | abpLocalization }}</h5>
        <a routerLink="new" class="btn btn-primary btn-sm">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewMaintenanceVisit' | abpLocalization }}
        </a>
      </div>
      <div class="card-body">
        <div class="row mb-3">
          <div class="col-md-3">
            <select class="form-select form-select-sm" [(ngModel)]="filterStatus" (change)="loadData()">
              <option [ngValue]="null">{{ 'MyERP::AllStatuses' | abpLocalization }}</option>
              <option [ngValue]="0">Open</option>
              <option [ngValue]="1">Partially Completed</option>
              <option [ngValue]="2">Completed</option>
              <option [ngValue]="3">Cancelled</option>
            </select>
          </div>
          <div class="col-md-3">
            <select class="form-select form-select-sm" [(ngModel)]="filterType" (change)="loadData()">
              <option value="">{{ 'MyERP::AllTypes' | abpLocalization }}</option>
              <option value="Scheduled">Scheduled</option>
              <option value="Unscheduled">Unscheduled</option>
              <option value="Breakdown">Breakdown</option>
            </select>
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-hover align-middle">
            <thead class="table-light">
              <tr>
                <th>{{ 'MyERP::VisitDate' | abpLocalization }}</th>
                <th>{{ 'MyERP::MaintenanceType' | abpLocalization }}</th>
                <th>{{ 'MyERP::Customer' | abpLocalization }}</th>
                <th>{{ 'MyERP::Status' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (visit of visits(); track visit.id) {
                <tr>
                  <td>{{ visit.visitDate | date:'mediumDate' }}</td>
                  <td>
                    <span class="badge" [class]="getTypeBadgeClass(visit.maintenanceType)">
                      {{ visit.maintenanceType }}
                    </span>
                  </td>
                  <td>{{ customerNames()[visit.customerId ?? ''] || '-' }}</td>
                  <td>
                    <app-status-badge [status]="getStatusLabel(visit.completionStatus)"
                      [variant]="getStatusVariant(visit.completionStatus)" />
                  </td>
                  <td class="text-end">
                    <a [routerLink]="[visit.id]" class="btn btn-sm btn-outline-primary">
                      <i class="bi bi-eye"></i>
                    </a>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="5" class="text-center text-muted py-4">
                    {{ 'MyERP::NoRecordsFound' | abpLocalization }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <app-pagination
          [totalCount]="totalCount()"
          [pageSize]="pageSize"
          [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      </div>
    </div>
  `,
})
export class MaintenanceVisitListComponent implements OnInit {
  private service = inject(MaintenanceService);
  private customerService = inject(CustomerService);
  private toaster = inject(ToasterService);

  visits = signal<MaintenanceVisitDto[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  customerNames = signal<Record<string, string>>({});

  filterStatus: number | null = null;
  filterType = '';
  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.loadCustomers();
    this.loadData();
  }

  private loadCustomers() {
    this.customerService.getList({ maxResultCount: 500 } as any).subscribe({
      next: (res) => {
        const map: Record<string, string> = {};
        (res.items ?? []).forEach((c: any) => { map[c.id] = c.customerName || c.id; });
        this.customerNames.set(map);
      },
    });
  }

  loadData() {
    this.loading.set(true);
    this.service.getVisitList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      completionStatus: this.filterStatus,
      maintenanceType: this.filterType || undefined,
    } as any).subscribe({
      next: (res) => {
        this.visits.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); },
    });
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  getStatusLabel(status: number): string {
    return ['Open', 'Partially Completed', 'Completed', 'Cancelled'][status] ?? 'Unknown';
  }

  getStatusVariant(status: number): string {
    return ['warning', 'info', 'success', 'danger'][status] ?? 'secondary';
  }

  getTypeBadgeClass(type: string): string {
    switch (type) {
      case 'Breakdown': return 'bg-danger text-white';
      case 'Unscheduled': return 'bg-warning text-dark';
      default: return 'bg-info text-white';
    }
  }
}
