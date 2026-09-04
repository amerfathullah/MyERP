import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BomStockAnalysisService } from '../../proxy/manufacturing/bom-stock-analysis.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-bom-stock-analysis',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fa fa-cubes me-2"></i>{{ 'BomStockAnalysis' | abpLocalization }}</h5>
        @if (result()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- BOM selector + Qty -->
        <div class="row g-2 mb-4">
          <div class="col-md-5">
            <label class="form-label">BOM</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedBomId">
              <option value="">{{ '::Select' | abpLocalization }}</option>
              @for (b of boms(); track b.id) {
                <option [value]="b.id">{{ b.bomNumber }} — {{ b.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Quantity' | abpLocalization }}</label>
            <input type="number" class="form-control form-control-sm" [(ngModel)]="requiredQty" min="1" step="1">
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm w-100" (click)="analyze()" [disabled]="!selectedBomId || isLoading()">
              <i class="fa fa-search me-1" [class.fa-spin]="isLoading()"></i>{{ 'Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (result(); as r) {
          <!-- Summary Cards -->
          <div class="row g-2 mb-3">
            <div class="col-md-4">
              <div class="border rounded p-3 text-center" [class.border-success]="r.allMaterialsSufficient" [class.border-danger]="!r.allMaterialsSufficient">
                <div class="fs-5 fw-bold" [class.text-success]="r.allMaterialsSufficient" [class.text-danger]="!r.allMaterialsSufficient">
                  {{ r.allMaterialsSufficient ? ('SufficientStock' | abpLocalization) : ('InsufficientStock' | abpLocalization) }}
                </div>
                <small class="text-muted">{{ 'MaterialAvailability' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-4">
              <div class="border rounded p-3 text-center">
                <div class="fs-3 fw-bold text-primary">{{ r.canManufactureQty | number:'1.0-0' }}</div>
                <small class="text-muted">{{ 'CanManufacture' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-4">
              <div class="border rounded p-3 text-center">
                <div class="fs-5 fw-bold">{{ r.materials.length }}</div>
                <small class="text-muted">Materials</small>
              </div>
            </div>
          </div>

          <!-- Materials Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover">
              <thead class="table-light">
                <tr>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th class="text-end">{{ 'RequiredQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'AvailableQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Shortage' | abpLocalization }}</th>
                  <th class="text-center">{{ 'Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (m of r.materials; track m.itemId) {
                  <tr [class.table-danger]="!m.isSufficient">
                    <td>{{ m.itemName }}</td>
                    <td class="text-end">{{ m.requiredQtyForBatch | number:'1.2-4' }}</td>
                    <td class="text-end">{{ m.availableQty | number:'1.2-4' }}</td>
                    <td class="text-end">
                      @if (m.shortage > 0) {
                        <span class="text-danger fw-bold">-{{ m.shortage | number:'1.2-4' }}</span>
                      } @else {
                        <span class="text-success">—</span>
                      }
                    </td>
                    <td class="text-center">
                      @if (m.isSufficient) {
                        <i class="fa fa-check-circle text-success"></i>
                      } @else {
                        <i class="fa fa-times-circle text-danger"></i>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else if (!isLoading()) {
          <p class="text-muted text-center py-4">{{ 'SelectBomToAnalyze' | abpLocalization }}</p>
        }
      </div>
    </div>
  `
})
export class BomStockAnalysisComponent implements OnInit {
  private bomAnalysisService = inject(BomStockAnalysisService);
  private mfgService = inject(ManufacturingService);

  boms = signal<any[]>([]);
  result = signal<any>(null);
  isLoading = signal(false);

  selectedBomId = '';
  requiredQty = 1;

  ngOnInit() {
    this.mfgService.getBomList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: (r) => {
        // Per ERPNext PR #58647 (commit a2071a6fdd): filter out cancelled/inactive BOMs
        const activeBoms = (r.items ?? []).filter((b: any) => b.isActive !== false && b.status !== 'Cancelled' && b.status !== 2);
        this.boms.set(activeBoms);
      },
      error: () => {}
    });
  }

  analyze() {
    if (!this.selectedBomId) return;
    this.isLoading.set(true);
    this.bomAnalysisService.getAnalysis({ bomId: this.selectedBomId, requiredQty: this.requiredQty } as any).subscribe({
      next: (r: any) => { this.result.set(r); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }

  exportCsv() {
    const r = this.result();
    if (!r) return;
    const rows = r.materials.map((m: any) => ({
      Item: m.itemName,
      RequiredQty: m.requiredQtyForBatch,
      AvailableQty: m.availableQty,
      Shortage: m.shortage,
      Status: m.isSufficient ? 'OK' : 'SHORT'
    }));
    exportToCsv(`bom-stock-analysis-${r.bomNumber}.csv`, rows, ['Item', 'RequiredQty', 'AvailableQty', 'Shortage', 'Status']);
  }
}
