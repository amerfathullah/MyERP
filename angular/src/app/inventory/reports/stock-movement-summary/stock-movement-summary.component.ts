import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StockLedgerService } from '../../../proxy/inventory/stock-ledger.service';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { DatePresetsComponent } from '../../../shared/components/date-presets/date-presets.component';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface StockMovementItem {
  itemId: string;
  itemCode: string;
  itemName: string;
  openingQty: number;
  stockInQty: number;
  stockOutQty: number;
  closingQty: number;
  stockInValue: number;
  stockOutValue: number;
  netMovement: number;
}

interface StockMovementSummary {
  fromDate: string;
  toDate: string;
  totalItems: number;
  totalStockIn: number;
  totalStockOut: number;
  totalStockInValue: number;
  totalStockOutValue: number;
  items: StockMovementItem[];
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe, DatePresetsComponent],
  selector: 'app-stock-movement-summary',
  template: `
    <div class="container-fluid">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4><i class="fa fa-exchange-alt me-2"></i>{{ '::StockMovementSummary' | abpLocalization }}</h4>
        @if (report()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card mb-3">
        <div class="card-body">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label">{{ '::From' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="generate()">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::To' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="generate()">
            </div>
            <div class="col-md-3">
              <app-date-presets (dateRange)="onDatePreset($event)"></app-date-presets>
            </div>
            <div class="col-md-3">
              <button class="btn btn-primary btn-sm w-100" (click)="generate()" [disabled]="isLoading()">
                <i class="fa fa-chart-bar me-1"></i>{{ '::Generate' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (report()) {
        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <div class="card border-start border-4 border-success">
              <div class="card-body py-2">
                <small class="text-muted">{{ '::StockIn' | abpLocalization }}</small>
                <h5 class="mb-0 text-success">{{ report()!.totalStockIn | number:'1.0-2' }}</h5>
                <small class="text-muted">{{ report()!.totalStockInValue | number:'1.2-2' }}</small>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-4 border-danger">
              <div class="card-body py-2">
                <small class="text-muted">{{ '::StockOut' | abpLocalization }}</small>
                <h5 class="mb-0 text-danger">{{ report()!.totalStockOut | number:'1.0-2' }}</h5>
                <small class="text-muted">{{ report()!.totalStockOutValue | number:'1.2-2' }}</small>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-4" [class.border-primary]="netMovement() >= 0" [class.border-warning]="netMovement() < 0">
              <div class="card-body py-2">
                <small class="text-muted">{{ '::NetMovement' | abpLocalization }}</small>
                <h5 class="mb-0" [class.text-primary]="netMovement() >= 0" [class.text-warning]="netMovement() < 0">
                  {{ netMovement() | number:'1.0-2' }}
                </h5>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-4 border-info">
              <div class="card-body py-2">
                <small class="text-muted">{{ '::Items' | abpLocalization }}</small>
                <h5 class="mb-0 text-info">{{ report()!.totalItems }}</h5>
              </div>
            </div>
          </div>
        </div>

        <div class="card">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover table-sm mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::OpeningQty' | abpLocalization }}</th>
                    <th class="text-end text-success">{{ '::StockIn' | abpLocalization }}</th>
                    <th class="text-end text-danger">{{ '::StockOut' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ClosingQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::NetMovement' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of report()!.items; track item.itemId) {
                    <tr>
                      <td>
                        <a [routerLink]="['/inventory/items', item.itemId, 'edit']" class="text-decoration-none">
                          {{ item.itemName }}
                        </a>
                        <small class="text-muted d-block">{{ item.itemCode }}</small>
                      </td>
                      <td class="text-end">{{ item.openingQty | number:'1.0-2' }}</td>
                      <td class="text-end text-success fw-semibold">
                        @if (item.stockInQty > 0) { +{{ item.stockInQty | number:'1.0-2' }} }
                        @else { — }
                      </td>
                      <td class="text-end text-danger fw-semibold">
                        @if (item.stockOutQty > 0) { -{{ item.stockOutQty | number:'1.0-2' }} }
                        @else { — }
                      </td>
                      <td class="text-end fw-bold" [class.text-danger]="item.closingQty < 0">
                        {{ item.closingQty | number:'1.0-2' }}
                      </td>
                      <td class="text-end">
                        <span [class.text-success]="item.netMovement > 0"
                              [class.text-danger]="item.netMovement < 0"
                              [class.text-muted]="item.netMovement === 0">
                          {{ item.netMovement > 0 ? '+' : '' }}{{ item.netMovement | number:'1.0-2' }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr>
                    <td>{{ '::Total' | abpLocalization }}</td>
                    <td class="text-end">—</td>
                    <td class="text-end text-success">+{{ report()!.totalStockIn | number:'1.0-2' }}</td>
                    <td class="text-end text-danger">-{{ report()!.totalStockOut | number:'1.0-2' }}</td>
                    <td class="text-end">—</td>
                    <td class="text-end" [class.text-success]="netMovement() >= 0" [class.text-danger]="netMovement() < 0">
                      {{ netMovement() > 0 ? '+' : '' }}{{ netMovement() | number:'1.0-2' }}
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        </div>
      } @else if (!isLoading()) {
        <div class="text-center text-muted py-5">
          <i class="fa fa-exchange-alt fa-3x mb-3 opacity-25"></i>
          <p>{{ '::NoMovementsFound' | abpLocalization }}</p>
        </div>
      }

      @if (isLoading()) {
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      }
    </div>
  `,
})
export class StockMovementSummaryComponent implements OnInit {
  private stockLedgerService = inject(StockLedgerService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  report = signal<StockMovementSummary | null>(null);
  isLoading = signal(false);

  netMovement = computed(() => {
    const r = this.report();
    return r ? r.totalStockIn - r.totalStockOut : 0;
  });

  ngOnInit(): void {
    const now = new Date();
    const monthAgo = new Date(now.getFullYear(), now.getMonth() - 1, now.getDate());
    this.fromDate = monthAgo.toISOString().slice(0, 10);
    this.toDate = now.toISOString().slice(0, 10);
    this.generate();
  }

  onDatePreset(range: { from: string; to: string }): void {
    this.fromDate = range.from;
    this.toDate = range.to;
    this.generate();
  }

  generate(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.isLoading.set(true);
    this.stockLedgerService.getStockMovementSummary(companyId, this.fromDate, this.toDate).subscribe({
      next: (data: any) => { this.report.set(data); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); },
    });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;
    exportToCsv(`stock-movement-${this.fromDate}-to-${this.toDate}.csv`, r.items.map(i => ({
      'Item Code': i.itemCode,
      'Item Name': i.itemName,
      'Opening Qty': i.openingQty,
      'Stock In': i.stockInQty,
      'Stock Out': i.stockOutQty,
      'Closing Qty': i.closingQty,
      'Net Movement': i.netMovement,
      'In Value': i.stockInValue,
      'Out Value': i.stockOutValue,
    })), ['Item Code', 'Item Name', 'Opening Qty', 'Stock In', 'Stock Out', 'Closing Qty', 'Net Movement', 'In Value', 'Out Value']);
  }
}
