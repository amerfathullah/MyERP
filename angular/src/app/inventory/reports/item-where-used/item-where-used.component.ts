import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemWhereUsedService } from '../../../proxy/inventory/item-where-used.service';
import type { ItemWhereUsedReportDto, ItemWhereUsedRowDto } from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-item-where-used',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-diagram-project me-2 text-primary"></i>{{ '::ItemWhereUsed' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ItemWhereUsedDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-5">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }} *</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId" (change)="loadData()">
              <option value="">Select an Item...</option>
              @for (item of items(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} — {{ item.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label small fw-semibold">Section Filter</label>
            <div class="btn-group btn-group-sm w-100" role="group">
              <button
                type="button"
                class="btn"
                [class.btn-primary]="selectedSection === ''"
                [class.btn-outline-primary]="selectedSection !== ''"
                (click)="setSection('')"
              >
                All
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-primary]="selectedSection === 'Where Used'"
                [class.btn-outline-primary]="selectedSection !== 'Where Used'"
                (click)="setSection('Where Used')"
              >
                {{ '::WhereUsed' | abpLocalization }}
                @if (data()) {
                  ({{ data()!.totalWhereUsedCount }})
                }
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-primary]="selectedSection === 'References'"
                [class.btn-outline-primary]="selectedSection !== 'References'"
                (click)="setSection('References')"
              >
                {{ '::References' | abpLocalization }}
                @if (data()) {
                  ({{ data()!.totalReferencesCount }})
                }
              </button>
            </div>
          </div>
          <div class="col-md-3">
            <button
              class="btn btn-primary btn-sm w-100"
              (click)="loadData()"
              [disabled]="isLoading() || !selectedItemId"
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
          <!-- KPI Summary Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card bg-primary-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalWhereUsed' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-primary">{{ d.totalWhereUsedCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-info-subtle border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">{{ '::TotalReferences' | abpLocalization }}</div>
                  <div class="h4 mb-0 fw-bold text-info">{{ d.totalReferencesCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-body-tertiary border-0 shadow-none h-100">
                <div class="card-body py-2">
                  <div class="text-muted small">Total Records</div>
                  <div class="h4 mb-0 fw-bold">{{ d.rows.length }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Grid Table -->
          <div class="table-responsive">
            <table class="table table-hover align-middle table-sm border">
              <thead class="table-light">
                <tr>
                  <th style="width: 120px;">Section</th>
                  <th>Reference Type</th>
                  <th>Document</th>
                  <th>Related Item</th>
                  <th class="text-end">Qty</th>
                  <th>UOM</th>
                  <th>Details / Status</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track (row.documentType + '-' + row.documentName + '-' + row.referenceType)) {
                  <tr>
                    <td>
                      @if (row.section === 'Where Used') {
                        <span class="badge bg-primary-subtle text-primary border border-primary-subtle">
                          Where Used
                        </span>
                      } @else {
                        <span class="badge bg-info-subtle text-info border border-info-subtle">
                          References
                        </span>
                      }
                    </td>
                    <td>
                      <span class="fw-semibold small">{{ row.referenceType }}</span>
                    </td>
                    <td>
                      <div class="fw-medium font-monospace small">{{ row.documentName }}</div>
                      <small class="text-muted">{{ row.documentType }}</small>
                    </td>
                    <td>
                      @if (row.relatedItemCode) {
                        <div class="fw-medium font-monospace small">{{ row.relatedItemCode }}</div>
                        <small class="text-muted">{{ row.relatedItemName }}</small>
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </td>
                    <td class="text-end font-monospace">
                      @if (row.quantity !== null && row.quantity !== undefined) {
                        {{ row.quantity | number:'1.2-2' }}
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </td>
                    <td class="small">
                      {{ row.uom ?? '—' }}
                    </td>
                    <td class="small">
                      <div class="d-flex align-items-center gap-1 flex-wrap">
                        @if (row.isDefault) {
                          <span class="badge bg-success-subtle text-success">Default</span>
                        }
                        @if (row.isActive !== null && row.isActive !== undefined) {
                          @if (row.isActive) {
                            <span class="badge bg-light text-success border">Active</span>
                          } @else {
                            <span class="badge bg-light text-danger border">Inactive</span>
                          }
                        }
                        @if (row.details) {
                          <span class="text-muted">{{ row.details }}</span>
                        }
                      </div>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="7" class="text-center text-muted py-5">
                      <i class="fa fa-info-circle fa-2x text-muted mb-2 d-block"></i>
                      No usages or references found for this item.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else {
          <div class="text-center text-muted py-5">
            <i class="fa fa-arrow-up fa-2x mb-3 text-secondary d-block"></i>
            Please select an item above and click Generate to see where it is used.
          </div>
        }
      </div>
    </div>
  `,
})
export class ItemWhereUsedComponent implements OnInit {
  private service = inject(ItemWhereUsedService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);

  selectedItemId = '';
  selectedSection = '';

  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  data = signal<ItemWhereUsedReportDto | null>(null);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.itemService.getList({ skipCount: 0, maxResultCount: 300, sorting: '' }).subscribe({
      next: (res) => {
        const list = (res.items ?? []).map((i: any) => ({
          id: i.id,
          itemCode: i.itemCode,
          itemName: i.itemName,
        }));
        this.items.set(list);
      },
    });
  }

  setSection(section: string): void {
    this.selectedSection = section;
    if (this.selectedItemId) {
      this.loadData();
    }
  }

  loadData(): void {
    if (!this.selectedItemId) return;
    const companyId = this.companyContext.currentCompanyId();

    this.isLoading.set(true);
    this.service
      .getReport({
        itemId: this.selectedItemId,
        companyId: companyId ? companyId : null,
        section: this.selectedSection ? this.selectedSection : null,
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
    const currentData = this.data();
    if (!currentData || !currentData.rows || currentData.rows.length === 0) return;

    exportToCsv(
      `item-where-used-${currentData.itemCode || 'report'}`,
      currentData.rows,
      [
        'section',
        'referenceType',
        'documentType',
        'documentName',
        'relatedItemCode',
        'relatedItemName',
        'quantity',
        'uom',
        'stockQuantity',
        'stockUom',
        'companyName',
        'details',
      ]
    );
  }
}
