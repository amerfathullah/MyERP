import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BomExplorerService } from '../../proxy/manufacturing/bom-explorer.service';
import type { BomExplorerDto, BomExplorerRowDto, BomLookupDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-bom-explorer',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-sitemap me-2 text-primary"></i>{{ '::BomExplorer' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::BomExplorerDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filter Bar -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-5">
            <label class="form-label small text-muted">{{ '::BOM' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedBomId" (ngModelChange)="onBomChange()">
              <option [ngValue]="null">-- Select BOM --</option>
              @for (b of bomList(); track b.id) {
                <option [ngValue]="b.id">
                  {{ b.bomNumber }} ({{ b.itemCode }} - {{ b.itemName }})
                </option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::ExplodedQuantity' | abpLocalization }}</label>
            <input
              type="number"
              class="form-control form-control-sm"
              min="0.01"
              step="1"
              [(ngModel)]="quantity"
            />
          </div>
          <div class="col-md-2 ms-auto">
            <button
              class="btn btn-primary btn-sm w-100"
              (click)="loadData()"
              [disabled]="isLoading() || !selectedBomId"
            >
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5">
            <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
          </div>
        } @else if (data(); as d) {
          <!-- Header KPI Banner -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="card border-primary text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Finished Good</div>
                  <div class="fw-bold text-primary text-truncate">{{ d.itemCode }}</div>
                  <div class="small text-muted text-truncate">{{ d.itemName }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TotalComponents' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-info">{{ d.totalComponents }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-secondary text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TotalSubBoms' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-secondary">{{ d.totalSubBoms }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-success text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Cost</div>
                  <div class="fs-4 fw-bold text-success">{{ d.totalCost | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Tree Table -->
          <div class="table-responsive">
            <table class="table table-striped table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 280px;">Item / Component</th>
                  <th class="text-center" style="width: 80px;">{{ '::BOMLevel' | abpLocalization }}</th>
                  <th>{{ '::SubBom' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                  <th>{{ '::Uom' | abpLocalization }}</th>
                  <th class="text-end">Rate</th>
                  <th class="text-end">Amount</th>
                  <th class="text-end">Available Stock</th>
                  <th class="text-center">Stock Status</th>
                </tr>
              </thead>
              <tbody>
                @if (!d.rows || d.rows.length === 0) {
                  <tr>
                    <td colspan="9" class="text-center text-muted py-4">
                      {{ '::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
                @for (row of d.rows; track $index) {
                  <tr>
                    <td>
                      <div class="d-flex align-items-center" [style.padding-left.px]="row.indent * 24">
                        @if (row.indent > 0) {
                          <i class="fa fa-level-up-alt fa-rotate-90 me-2 text-muted"></i>
                        }
                        @if (row.subBomId) {
                          <i class="fa fa-folder-tree me-2 text-warning"></i>
                        } @else {
                          <i class="fa fa-cube me-2 text-secondary"></i>
                        }
                        <div>
                          <strong>{{ row.itemCode }}</strong>
                          @if (row.isPhantom) {
                            <span class="badge bg-info ms-1">{{ '::IsPhantomItem' | abpLocalization }}</span>
                          }
                          @if (row.doNotExplode) {
                            <span class="badge bg-secondary ms-1">No Explode</span>
                          }
                          @if (row.itemName && row.itemName !== row.itemCode) {
                            <div class="small text-muted">{{ row.itemName }}</div>
                          }
                        </div>
                      </div>
                    </td>
                    <td class="text-center">
                      <span class="badge bg-light text-dark border">L{{ row.bomLevel }}</span>
                    </td>
                    <td>
                      @if (row.subBomNumber) {
                        <span class="badge bg-primary-subtle text-primary border">
                          {{ row.subBomNumber }}
                        </span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-end fw-bold">{{ row.qty | number:'1.2-4' }}</td>
                    <td>{{ row.uom || '-' }}</td>
                    <td class="text-end">{{ row.rate | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold">{{ row.amount | number:'1.2-2' }}</td>
                    <td class="text-end" [class.text-danger]="!row.hasSufficientStock">
                      {{ row.availableQty | number:'1.2-2' }}
                    </td>
                    <td class="text-center">
                      @if (row.hasSufficientStock) {
                        <span class="badge bg-success-subtle text-success">
                          {{ '::SufficientStock' | abpLocalization }}
                        </span>
                      } @else {
                        <span class="badge bg-danger-subtle text-danger">
                          {{ '::StockShortage' | abpLocalization }}
                        </span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class BomExplorerComponent implements OnInit {
  private service = inject(BomExplorerService);
  private companyContext = inject(CompanyContextService);

  bomList = signal<BomLookupDto[]>([]);
  selectedBomId: string | null = null;
  quantity = 1;

  data = signal<BomExplorerDto | null>(null);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    this.loadBomList();
  }

  loadBomList(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.service.getBomList(companyId).subscribe({
      next: (boms) => {
        this.bomList.set(boms);
        if (boms.length > 0 && !this.selectedBomId) {
          const defaultBom = boms.find((b) => b.isDefault) || boms[0];
          this.selectedBomId = defaultBom.id;
          this.loadData();
        }
      },
    });
  }

  onBomChange(): void {
    if (this.selectedBomId) {
      this.loadData();
    }
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.selectedBomId) return;

    this.isLoading.set(true);
    this.service
      .getExplorer({
        companyId,
        bomId: this.selectedBomId,
        quantity: this.quantity,
      })
      .subscribe({
        next: (res) => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows || d.rows.length === 0) return;

    exportToCsv('bom_explorer.csv', d.rows, [
      'bomLevel',
      'itemCode',
      'itemName',
      'subBomNumber',
      'qty',
      'uom',
      'rate',
      'amount',
      'availableQty',
      'hasSufficientStock',
    ]);
  }
}
