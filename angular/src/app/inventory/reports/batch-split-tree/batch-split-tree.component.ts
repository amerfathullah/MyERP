import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BatchService } from '../../../proxy/inventory/batch.service';
import type {
  BatchSplitTreeNodeDto,
  ItemDto,
  BatchDto,
} from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface FlatBatchNode extends BatchSplitTreeNodeDto {
  hasChildren: boolean;
  expanded: boolean;
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-batch-split-tree',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-code-branch me-2 text-primary"></i>{{ '::BatchSplitTree' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::BatchSplitTreeDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (flatNodes().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId" (change)="loadData()">
              <option value="">{{ '::AllItems' | abpLocalization }}</option>
              @for (item of batchItems(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <button class="btn btn-outline-secondary btn-sm w-100" (click)="toggleAll()" [disabled]="isLoading() || flatNodes().length === 0">
              <i class="fa me-1" [class.fa-compress]="allExpanded()" [class.fa-expand]="!allExpanded()"></i>
              {{ (allExpanded() ? '::CollapseAll' : '::ExpandAll') | abpLocalization }}
            </button>
          </div>
          <div class="col-md-2 col-sm-6">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (treeData().length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-6 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalRootBatches' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ rootCount() }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-6 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalChildBatches' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">
                    {{ childCount() }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Tree Table -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ '::Loading' | abpLocalization }}</span>
            </div>
          </div>
        } @else if (filteredNodes().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-code-branch fa-3x mb-3 text-secondary opacity-50"></i>
            <p class="mb-0">{{ '::NoBatchSplitTreeFound' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle border">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Batch' | abpLocalization }}</th>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th class="text-end">{{ '::BatchQty' | abpLocalization }}</th>
                  <th>{{ '::UOM' | abpLocalization }}</th>
                  <th>{{ '::CreatedVia' | abpLocalization }}</th>
                  <th>{{ '::Reference' | abpLocalization }}</th>
                  <th>{{ '::ManufacturingDate' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (node of filteredNodes(); track node.batchId) {
                  <tr [class.table-active]="(node.indent ?? 0) === 0">
                    <td>
                      <div class="d-flex align-items-center" [style.padding-left.px]="(node.indent ?? 0) * 24">
                        @if (node.hasChildren) {
                          <button
                            type="button"
                            class="btn btn-sm btn-link p-0 me-2 text-secondary text-decoration-none"
                            (click)="toggleNode(node)"
                          >
                            <i class="fa fa-fw" [class.fa-chevron-down]="node.expanded" [class.fa-chevron-right]="!node.expanded"></i>
                          </button>
                        } @else {
                          <span class="d-inline-block me-2 text-muted" style="width: 16px; text-align: center;">•</span>
                        }
                        <span
                          class="badge"
                          [class.bg-primary]="(node.indent ?? 0) === 0"
                          [class.bg-secondary-subtle]="(node.indent ?? 0) > 0"
                          [class.text-secondary]="(node.indent ?? 0) > 0"
                        >
                          <i class="fa fa-tag me-1"></i>{{ node.batchNo }}
                        </span>
                      </div>
                    </td>
                    <td class="fw-semibold">{{ node.itemCode }}</td>
                    <td>{{ node.itemName }}</td>
                    <td class="text-end font-monospace fw-semibold">{{ node.batchQty | number: '1.2-2' }}</td>
                    <td class="small text-muted">{{ node.stockUom || '-' }}</td>
                    <td>
                      @if (node.referenceDocType) {
                        <span class="badge bg-light text-dark border">{{ node.referenceDocType }}</span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="small font-monospace text-muted">{{ node.referenceName || '-' }}</td>
                    <td class="small">{{ node.manufacturingDate ? (node.manufacturingDate | date: 'yyyy-MM-dd') : '-' }}</td>
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
export class BatchSplitTreeComponent implements OnInit {
  private batchService = inject(BatchService);
  private itemService = inject(ItemService);

  batchItems = signal<ItemDto[]>([]);
  treeData = signal<BatchSplitTreeNodeDto[]>([]);
  flatNodes = signal<FlatBatchNode[]>([]);
  isLoading = signal(false);

  selectedItemId = '';
  searchTerm = '';
  allExpanded = signal(true);

  rootCount = computed(() => this.treeData().length);
  childCount = computed(() => this.flatNodes().filter(n => (n.indent ?? 0) > 0).length);

  filteredNodes = computed(() => {
    const nodes = this.flatNodes();
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return nodes;

    return nodes.filter(
      n =>
        (n.batchNo && n.batchNo.toLowerCase().includes(term)) ||
        (n.itemCode && n.itemCode.toLowerCase().includes(term)) ||
        (n.itemName && n.itemName.toLowerCase().includes(term)) ||
        (n.referenceName && n.referenceName.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadDropdowns();
    this.loadData();
  }

  loadDropdowns(): void {
    this.itemService.getList({ hasBatchNo: true, maxResultCount: 500 } as any).subscribe({
      next: res => {
        this.batchItems.set(res.items ?? []);
      },
      error: () => {},
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    this.batchService
      .getBatchSplitTree({
        itemId: this.selectedItemId || null,
        batchId: null,
      })
      .subscribe({
        next: res => {
          this.treeData.set(res ?? []);
          const flattened = this.flattenTree(res ?? []);
          this.flatNodes.set(flattened);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  private flattenTree(nodes: BatchSplitTreeNodeDto[]): FlatBatchNode[] {
    const result: FlatBatchNode[] = [];
    const traverse = (nodeList: BatchSplitTreeNodeDto[]) => {
      for (const node of nodeList) {
        const hasChildren = !!node.children && node.children.length > 0;
        result.push({
          ...node,
          hasChildren,
          expanded: true,
        });
        if (hasChildren) {
          traverse(node.children!);
        }
      }
    };
    traverse(nodes);
    return result;
  }

  toggleNode(node: FlatBatchNode): void {
    node.expanded = !node.expanded;
  }

  toggleAll(): void {
    const nextState = !this.allExpanded();
    this.allExpanded.set(nextState);
    this.flatNodes.update(nodes =>
      nodes.map(n => ({
        ...n,
        expanded: nextState,
      }))
    );
  }

  exportCsv(): void {
    const nodes = this.filteredNodes();
    if (!nodes.length) return;

    const exportRows = nodes.map(n => ({
      Indent: n.indent ?? 0,
      BatchNo: n.batchNo,
      ItemCode: n.itemCode,
      ItemName: n.itemName,
      BatchQty: n.batchQty,
      StockUOM: n.stockUom,
      CreatedVia: n.referenceDocType,
      Reference: n.referenceName,
      ManufacturingDate: n.manufacturingDate,
    }));

    const columns = [
      'Indent',
      'BatchNo',
      'ItemCode',
      'ItemName',
      'BatchQty',
      'StockUOM',
      'CreatedVia',
      'Reference',
      'ManufacturingDate',
    ];

    const filename = `Batch_Split_Tree_${new Date().toISOString().slice(0, 10)}.csv`;
    exportToCsv(filename, exportRows, columns);
  }
}
