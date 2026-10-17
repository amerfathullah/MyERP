import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BomOperationsTimeService } from '../../proxy/manufacturing/bom-operations-time.service';
import type { BomOperationsTimeReportDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-bom-operations-time',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0"><i class="fa fa-clock me-2 text-primary"></i>{{ '::BomOperationsTime' | abpLocalization }}</h5>
          <small class="text-muted">{{ '::BomOperationsTimeDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Actions & Refresh -->
        <div class="d-flex justify-content-end mb-3">
          <button class="btn btn-primary btn-sm" (click)="loadData()" [disabled]="isLoading()">
            <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Refresh' | abpLocalization }}
          </button>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5">
            <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
          </div>
        } @else if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-4">
              <div class="card border-primary text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Active BOMs</div>
                  <div class="fs-4 fw-bold text-primary">{{ d.totalBoms }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Operations</div>
                  <div class="fs-4 fw-bold text-info">{{ d.totalOperations }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4">
              <div class="card border-warning text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TimeInMins' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-warning">{{ d.totalTimeInMinutes | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>BOM Number</th>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::UOM' | abpLocalization }}</th>
                  <th>{{ '::Operation' | abpLocalization }}</th>
                  <th>{{ '::Workstation' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TimeInMins' | abpLocalization }}</th>
                  <th class="text-center">{{ '::SubAssemblyBomCount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.bomId + '-' + row.operationId + '-' + row.workstationId) {
                  <tr>
                    <td><strong>{{ row.bomNumber }}</strong></td>
                    <td><code>{{ row.itemCode }}</code></td>
                    <td>{{ row.itemName }}</td>
                    <td><span class="badge bg-secondary">{{ row.uom }}</span></td>
                    <td><strong>{{ row.operationName }}</strong></td>
                    <td>{{ row.workstationName || '-' }}</td>
                    <td class="text-end fw-medium">{{ row.timeInMins | number:'1.2-2' }}</td>
                    <td class="text-center">
                      <span class="badge" [class.bg-info]="row.usedAsSubassemblyItems > 0" [class.bg-light]="row.usedAsSubassemblyItems === 0" [class.text-dark]="row.usedAsSubassemblyItems === 0">
                        {{ row.usedAsSubassemblyItems }}
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="8" class="text-center py-4 text-muted">{{ '::NoDataAvailable' | abpLocalization }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `
})
export class BomOperationsTimeComponent implements OnInit {
  private service = inject(BomOperationsTimeService);
  private companyContext = inject(CompanyContextService);

  isLoading = signal(false);
  data = signal<BomOperationsTimeReportDto | null>(null);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service.getReport({
      companyId,
    }).subscribe({
      next: (res) => {
        this.data.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows.length) return;

    const exportRows = d.rows.map(r => ({
      BomNumber: r.bomNumber,
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      Uom: r.uom,
      Operation: r.operationName,
      Workstation: r.workstationName ?? '',
      TimeInMins: r.timeInMins,
      SubAssemblyBomCount: r.usedAsSubassemblyItems,
    }));

    const columns = [
      'BomNumber',
      'ItemCode',
      'ItemName',
      'Uom',
      'Operation',
      'Workstation',
      'TimeInMins',
      'SubAssemblyBomCount',
    ];

    exportToCsv(`bom-operations-time-${new Date().toISOString().substring(0, 10)}.csv`, exportRows, columns);
  }
}
