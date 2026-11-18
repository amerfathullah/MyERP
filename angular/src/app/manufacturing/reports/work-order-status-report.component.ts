import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, map } from 'rxjs';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../proxy/core/company.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { CompanyDto } from '../../proxy/core/models';
import type {
  CompletedWorkOrderItemDto,
  OpenWorkOrderItemDto,
  WorkOrderInProgressItemDto,
} from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

export type WorkOrderStatusReportMode = 'open' | 'in-progress' | 'completed';

export type WorkOrderStatusRow = OpenWorkOrderItemDto &
  WorkOrderInProgressItemDto &
  CompletedWorkOrderItemDto;

export function buildWorkOrderStatusCsvRows(
  rows: WorkOrderStatusRow[],
): Record<string, string | number>[] {
  return rows.map(r => ({
    'Work Order': r.workOrderNumber ?? '',
    Date: r.creationTime ? r.creationTime.split('T')[0] : '',
    Item: r.itemCode ?? '',
    'Item Name': r.itemName ?? '',
    'To Produce': r.quantity ?? 0,
    Produced: r.producedQuantity ?? 0,
    'Process Loss Qty': r.processLossQty ?? 0,
    Status: r.statusLabel ?? '',
    'Stock Entries': r.stockEntryCount ?? 0,
  }));
}

@Component({
  selector: 'app-work-order-status-report',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="titleKey() | abpLocalization">
      <div class="card mb-4">
        <div class="card-body">
          <div class="row g-3 align-items-end">
            <div class="col-md-3">
              <label class="form-label">{{ '::Company' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="companyId" (change)="loadData()">
                <option value="">{{ '::Select' | abpLocalization }}</option>
                @for (c of companies(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-5">
              <label class="form-label d-block">{{ '::Status' | abpLocalization }}</label>
              <div class="btn-group" role="group">
                <button
                  type="button"
                  class="btn btn-sm"
                  [class.btn-primary]="mode() === 'open'"
                  [class.btn-outline-primary]="mode() !== 'open'"
                  (click)="switchMode('open')"
                >
                  {{ '::OpenWorkOrders' | abpLocalization }}
                </button>
                <button
                  type="button"
                  class="btn btn-sm"
                  [class.btn-primary]="mode() === 'in-progress'"
                  [class.btn-outline-primary]="mode() !== 'in-progress'"
                  (click)="switchMode('in-progress')"
                >
                  {{ '::WorkOrdersInProgress' | abpLocalization }}
                </button>
                <button
                  type="button"
                  class="btn btn-sm"
                  [class.btn-primary]="mode() === 'completed'"
                  [class.btn-outline-primary]="mode() !== 'completed'"
                  (click)="switchMode('completed')"
                >
                  {{ '::CompletedWorkOrders' | abpLocalization }}
                </button>
              </div>
            </div>
            <div class="col-md-2">
              <button
                type="button"
                class="btn btn-primary w-100"
                [disabled]="isLoading() || !companyId"
                (click)="loadData()"
              >
                <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
              </button>
            </div>
            <div class="col-md-2">
              <button
                type="button"
                class="btn btn-outline-success w-100"
                [disabled]="!rows().length"
                (click)="exportCsv()"
              >
                <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (isLoading()) {
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      } @else {
        <div class="row g-3 mb-4">
          <div class="col-md-4">
            <div class="card h-100">
              <div class="card-body">
                <div class="text-muted small">{{ '::TotalWorkOrders' | abpLocalization }}</div>
                <div class="fs-3 fw-bold font-monospace">{{ rows().length }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card h-100">
              <div class="card-body">
                <div class="text-muted small">{{ '::ToProduce' | abpLocalization }}</div>
                <div class="fs-3 fw-bold font-monospace">{{ totalToProduce() | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card h-100">
              <div class="card-body">
                <div class="text-muted small">{{ '::Produced' | abpLocalization }}</div>
                <div class="fs-3 fw-bold font-monospace text-success">{{ totalProduced() | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-body p-0">
            @if (!rows().length) {
              <div class="text-center py-4 text-muted">{{ '::NoData' | abpLocalization }}</div>
            } @else {
              <div class="table-responsive">
                <table class="table table-hover table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th class="ps-3">{{ '::WorkOrder' | abpLocalization }}</th>
                      <th>{{ '::Date' | abpLocalization }}</th>
                      <th>{{ '::Item' | abpLocalization }}</th>
                      <th class="text-end">{{ '::ToProduce' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Produced' | abpLocalization }}</th>
                      @if (mode() === 'in-progress') {
                        <th class="text-end">{{ '::StockEntries' | abpLocalization }}</th>
                      }
                      <th class="pe-3">{{ '::Status' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (row of rows(); track row.workOrderId) {
                      <tr>
                        <td class="ps-3 font-monospace">
                          <a [routerLink]="['/manufacturing/work-orders', row.workOrderId]">
                            {{ row.workOrderNumber }}
                          </a>
                        </td>
                        <td>{{ row.creationTime | date:'yyyy-MM-dd' }}</td>
                        <td>{{ row.itemCode }} - {{ row.itemName }}</td>
                        <td class="text-end font-monospace">{{ (row.quantity ?? 0) | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace">{{ (row.producedQuantity ?? 0) | number:'1.2-2' }}</td>
                        @if (mode() === 'in-progress') {
                          <td class="text-end font-monospace">{{ row.stockEntryCount ?? 0 }}</td>
                        }
                        <td class="pe-3">{{ row.statusLabel }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class WorkOrderStatusReportComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private manufacturingService = inject(ManufacturingService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  mode = signal<WorkOrderStatusReportMode>('open');
  companies = signal<CompanyDto[]>([]);
  companyId = '';
  rows = signal<WorkOrderStatusRow[]>([]);
  isLoading = signal(false);

  titleKey = computed(() => {
    switch (this.mode()) {
      case 'in-progress':
        return '::WorkOrdersInProgress';
      case 'completed':
        return '::CompletedWorkOrders';
      default:
        return '::OpenWorkOrders';
    }
  });

  totalToProduce = computed(() => this.rows().reduce((acc, r) => acc + (r.quantity ?? 0), 0));
  totalProduced = computed(() => this.rows().reduce((acc, r) => acc + (r.producedQuantity ?? 0), 0));

  ngOnInit(): void {
    const initialMode = this.route.snapshot.data?.['reportMode'] as WorkOrderStatusReportMode | undefined;
    if (initialMode === 'open' || initialMode === 'in-progress' || initialMode === 'completed') {
      this.mode.set(initialMode);
    }

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(res => {
      const list = res.items ?? [];
      this.companies.set(list);
      const defaultId = this.companyContext.currentCompanyId() || list[0]?.id || '';
      if (defaultId) {
        this.companyId = defaultId;
        this.loadData();
      }
    });
  }

  switchMode(next: WorkOrderStatusReportMode): void {
    if (this.mode() === next) return;
    this.mode.set(next);
    if (this.companyId) {
      this.loadData();
    }
  }

  loadData(): void {
    if (!this.companyId) return;
    this.isLoading.set(true);

    const req$: Observable<WorkOrderStatusRow[]> =
      this.mode() === 'completed'
        ? this.manufacturingService
            .getCompletedWorkOrdersReport(this.companyId)
            .pipe(map(res => (res ?? []) as WorkOrderStatusRow[]))
        : this.mode() === 'in-progress'
          ? this.manufacturingService
              .getWorkOrdersInProgressReport(this.companyId)
              .pipe(map(res => (res ?? []) as WorkOrderStatusRow[]))
          : this.manufacturingService
              .getOpenWorkOrdersReport(this.companyId)
              .pipe(map(res => (res ?? []) as WorkOrderStatusRow[]));

    req$.subscribe({
      next: res => {
        this.rows.set(res);
        this.isLoading.set(false);
      },
      error: () => {
        this.toaster.error('Failed to load Work Orders report.');
        this.isLoading.set(false);
      },
    });
  }

  exportCsv(): void {
    const list = this.rows();
    if (!list.length) return;
    const csvRows = buildWorkOrderStatusCsvRows(list);
    exportToCsv(`work-orders-${this.mode()}`, csvRows, Object.keys(csvRows[0]));
  }
}
