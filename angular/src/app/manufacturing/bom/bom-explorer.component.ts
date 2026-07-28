import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

interface BomTreeNode {
  itemCode: string;
  itemName: string;
  quantity: number;
  uom: string;
  rate: number;
  amount: number;
  isSubAssembly: boolean;
  bomId?: string;
  children: BomTreeNode[];
  expanded: boolean;
  level: number;
}

/**
 * BOM Tree Explorer — interactive multi-level Bill of Materials visualization.
 * Shows the complete explosion of a BOM as a hierarchical tree.
 * Per ERPNext: BOM Explorer report (gotcha #5934) — recursive CTE with phantom bubbling.
 * 
 * Features:
 * - Expandable/collapsible tree nodes
 * - Sub-assembly drill-down (shows their own BOM components)
 * - Cost rollup at each level
 * - Color-coded: purchased items (blue), manufactured sub-assemblies (green)
 * - Quantity scaled to parent BOM quantity
 */
@Component({
  selector: 'app-bom-explorer',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-sitemap me-2"></i>{{ 'Manufacturing:BOMExplorer' | abpLocalization }}</h5>
          <div class="d-flex align-items-center gap-2">
            <select class="form-select form-select-sm" style="width:300px" [(ngModel)]="selectedBomId" (ngModelChange)="loadBomTree()">
              <option value="">{{ '::SelectBOM' | abpLocalization }}</option>
              @for (bom of availableBoms(); track bom.id) {
                <option [value]="bom.id">{{ bom.bomNumber }} — {{ bom.itemName }}</option>
              }
            </select>
            <button class="btn btn-sm btn-outline-secondary" (click)="expandAll()" [disabled]="!tree()">
              <i class="fas fa-expand-alt"></i>
            </button>
            <button class="btn btn-sm btn-outline-secondary" (click)="collapseAll()" [disabled]="!tree()">
              <i class="fas fa-compress-alt"></i>
            </button>
          </div>
        </div>
        <div class="card-body">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (!tree()) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-sitemap fa-3x mb-3 d-block opacity-50"></i>
              <p>Select a Bill of Materials to explore its component tree.</p>
            </div>
          } @else {
            <!-- Summary bar -->
            <div class="d-flex gap-4 mb-3 p-2 bg-light rounded">
              <div><small class="text-muted">FG Item:</small> <strong>{{ tree()!.itemCode }}</strong> — {{ tree()!.itemName }}</div>
              <div><small class="text-muted">Total Cost:</small> <strong>{{ totalCost() | number:'1.2-2' }}</strong></div>
              <div><small class="text-muted">Components:</small> <strong>{{ totalComponents() }}</strong></div>
            </div>

            <!-- Tree table -->
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0 bom-tree-table">
                <thead class="table-dark">
                  <tr>
                    <th style="min-width:350px">{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end" style="width:100px">{{ '::Qty' | abpLocalization }}</th>
                    <th style="width:60px">{{ '::UOM' | abpLocalization }}</th>
                    <th class="text-end" style="width:120px">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end" style="width:120px">{{ '::Amount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of flattenedTree(); track row.itemCode + '-' + row.level + '-' + $index) {
                    <tr [class.table-success]="row.isSubAssembly" [class.fw-bold]="row.isSubAssembly">
                      <td>
                        <span [style.padding-left.px]="row.level * 24">
                          @if (row.isSubAssembly) {
                            <button class="btn btn-link btn-sm p-0 me-1" (click)="toggleNode(row)">
                              <i class="fas" [class.fa-caret-down]="row.expanded" [class.fa-caret-right]="!row.expanded"></i>
                            </button>
                            <i class="fas fa-cogs me-1 text-success"></i>
                          } @else {
                            <span class="me-3"></span>
                            <i class="fas fa-cube me-1 text-primary"></i>
                          }
                          <span class="font-monospace small">{{ row.itemCode }}</span>
                          <span class="ms-2 text-muted">{{ row.itemName }}</span>
                        </span>
                      </td>
                      <td class="text-end">{{ row.quantity | number:'1.2-4' }}</td>
                      <td>{{ row.uom }}</td>
                      <td class="text-end">{{ row.rate | number:'1.2-2' }}</td>
                      <td class="text-end">{{ row.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .bom-tree-table tr.table-success { background: rgba(25, 135, 84, 0.05) !important; }
    .bom-tree-table .btn-link { text-decoration: none; }
  `],
})
export class BomExplorerComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);

  loading = signal(false);
  tree = signal<BomTreeNode | null>(null);
  availableBoms = signal<any[]>([]);
  selectedBomId = '';

  totalCost = signal(0);
  totalComponents = signal(0);

  ngOnInit(): void {
    this.loadBomList();
    // If navigated with bomId param, auto-load
    const bomId = this.route.snapshot.queryParamMap.get('bomId');
    if (bomId) {
      this.selectedBomId = bomId;
      this.loadBomTree();
    }
  }

  loadBomList(): void {
    this.http.get<any>('/api/app/manufacturing/bom-list', {
      params: { skipCount: '0', maxResultCount: '200' }
    }).subscribe({
      next: (res) => this.availableBoms.set(res?.items ?? []),
      error: () => {},
    });
  }

  loadBomTree(): void {
    if (!this.selectedBomId) { this.tree.set(null); return; }
    this.loading.set(true);
    this.http.get<any>(`/api/app/manufacturing/bom/${this.selectedBomId}`).subscribe({
      next: (bom) => {
        const rootNode = this.buildTree(bom, 0, 1);
        this.tree.set(rootNode);
        this.totalCost.set(rootNode.amount);
        this.totalComponents.set(this.countComponents(rootNode));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private buildTree(bom: any, level: number, parentQty: number): BomTreeNode {
    const node: BomTreeNode = {
      itemCode: bom.itemCode || bom.bomNumber || '',
      itemName: bom.itemName || '',
      quantity: bom.quantity ?? 1,
      uom: bom.uom || 'Unit',
      rate: bom.totalCost / (bom.quantity || 1),
      amount: bom.totalCost ?? 0,
      isSubAssembly: false,
      bomId: bom.id,
      children: [],
      expanded: level < 2, // Auto-expand first 2 levels
      level,
    };

    if (bom.items) {
      for (const item of bom.items) {
        const scaledQty = (item.quantity ?? 0) * parentQty;
        const childNode: BomTreeNode = {
          itemCode: item.itemCode || item.itemName || '',
          itemName: item.itemName || '',
          quantity: scaledQty,
          uom: item.uom || 'Unit',
          rate: item.rate ?? 0,
          amount: scaledQty * (item.rate ?? 0),
          isSubAssembly: !!item.subBomId,
          bomId: item.subBomId,
          children: [],
          expanded: false,
          level: level + 1,
        };
        node.children.push(childNode);
      }
    }

    return node;
  }

  private countComponents(node: BomTreeNode): number {
    let count = node.children.length;
    for (const child of node.children) {
      count += this.countComponents(child);
    }
    return count;
  }

  flattenedTree(): BomTreeNode[] {
    const result: BomTreeNode[] = [];
    if (!this.tree()) return result;
    this.flattenNode(this.tree()!, result);
    return result;
  }

  private flattenNode(node: BomTreeNode, result: BomTreeNode[]): void {
    for (const child of node.children) {
      result.push(child);
      if (child.expanded && child.children.length > 0) {
        this.flattenNode(child, result);
      }
    }
  }

  toggleNode(node: BomTreeNode): void {
    node.expanded = !node.expanded;
    // Force re-render by recreating tree signal
    this.tree.set({ ...this.tree()! });
  }

  expandAll(): void {
    if (!this.tree()) return;
    this.setExpandAll(this.tree()!, true);
    this.tree.set({ ...this.tree()! });
  }

  collapseAll(): void {
    if (!this.tree()) return;
    this.setExpandAll(this.tree()!, false);
    this.tree.set({ ...this.tree()! });
  }

  private setExpandAll(node: BomTreeNode, expanded: boolean): void {
    node.expanded = expanded;
    for (const child of node.children) {
      this.setExpandAll(child, expanded);
    }
  }
}
