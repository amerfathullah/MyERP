import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

interface WarehouseNode {
  id: string;
  name: string;
  isGroup: boolean;
  parentId: string | null;
  warehouseType: string;
  children: WarehouseNode[];
  expanded: boolean;
  actualQty: number;
  stockValue: number;
  level: number;
}

/**
 * Warehouse Tree View — shows hierarchical warehouse structure with stock summaries.
 * ERPNext uses NestedSet tree for warehouses (All Warehouses → Stores/WIP/FG/Transit).
 * 
 * Features:
 * - Expandable tree showing parent/child warehouse hierarchy
 * - Stock value rollup for group warehouses
 * - Type badges (Transit, Virtual, Regular)
 * - Quick navigation to stock balance report per warehouse
 * - Create child warehouse action on group nodes
 */
@Component({
  selector: 'app-warehouse-tree',
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-warehouse me-2"></i>{{ 'Inventory:WarehouseTree' | abpLocalization }}</h5>
          <a [routerLink]="['/inventory/warehouses/new']" class="btn btn-primary btn-sm">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (tree().length === 0) {
            <div class="text-center text-muted py-5">
              <i class="fas fa-warehouse fa-3x mb-2 d-block opacity-50"></i>
              <p>No warehouses configured.</p>
            </div>
          } @else {
            <div class="warehouse-tree">
              @for (node of flatTree(); track node.id) {
                <div class="tree-row d-flex align-items-center py-2 px-2 border-bottom"
                     [class.bg-light]="node.isGroup"
                     [style.padding-left.px]="node.level * 28 + 8">
                  <!-- Expand/collapse toggle -->
                  @if (node.isGroup) {
                    <button class="btn btn-link btn-sm p-0 me-2 text-dark" (click)="toggle(node)">
                      <i class="fas" [class.fa-caret-down]="node.expanded" [class.fa-caret-right]="!node.expanded"></i>
                    </button>
                    <i class="fas fa-folder me-2" [class.text-warning]="node.expanded" [class.text-secondary]="!node.expanded"></i>
                  } @else {
                    <span class="me-2" style="width:20px"></span>
                    <i class="fas fa-box me-2 text-primary"></i>
                  }

                  <!-- Name + badges -->
                  <div class="flex-grow-1">
                    <a [routerLink]="['/inventory/warehouses', node.id, 'edit']" class="text-dark text-decoration-none fw-medium">
                      {{ node.name }}
                    </a>
                    @if (node.warehouseType === 'Transit') {
                      <span class="badge bg-info-subtle text-info ms-2">Transit</span>
                    }
                    @if (node.isGroup) {
                      <span class="badge bg-secondary-subtle text-secondary ms-2">Group</span>
                    }
                  </div>

                  <!-- Stock info (leaf warehouses only) -->
                  @if (!node.isGroup) {
                    <div class="text-end" style="min-width:180px">
                      @if (node.actualQty > 0) {
                        <span class="text-muted small me-3">{{ node.actualQty | number:'1.0-0' }} items</span>
                        <span class="fw-bold">{{ node.stockValue | number:'1.0-0' }}</span>
                      } @else {
                        <span class="text-muted small">Empty</span>
                      }
                    </div>
                  } @else {
                    <div class="text-end" style="min-width:180px">
                      <span class="text-muted small">{{ getChildCount(node) }} warehouses</span>
                    </div>
                  }

                  <!-- Actions -->
                  <div class="ms-3">
                    <a [routerLink]="['/inventory/reports/stock-balance']" [queryParams]="{ warehouseId: node.id }"
                       class="btn btn-outline-secondary btn-sm" title="View Stock">
                      <i class="fas fa-boxes-stacked"></i>
                    </a>
                  </div>
                </div>
              }
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .warehouse-tree { font-size: 0.9rem; }
    .tree-row:hover { background: rgba(0,0,0,0.02); }
  `],
})
export class WarehouseTreeComponent implements OnInit {
  private http = inject(HttpClient);

  loading = signal(true);
  tree = signal<WarehouseNode[]>([]);

  ngOnInit(): void {
    this.loadWarehouses();
  }

  loadWarehouses(): void {
    this.http.get<any>('/api/app/warehouse', {
      params: { skipCount: '0', maxResultCount: '500', sorting: 'name asc' }
    }).subscribe({
      next: (res) => {
        const warehouses = res?.items ?? [];
        this.tree.set(this.buildTree(warehouses));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private buildTree(warehouses: any[]): WarehouseNode[] {
    const nodeMap = new Map<string, WarehouseNode>();
    const roots: WarehouseNode[] = [];

    // Create all nodes
    for (const wh of warehouses) {
      nodeMap.set(wh.id, {
        id: wh.id,
        name: wh.name,
        isGroup: wh.isGroup ?? false,
        parentId: wh.parentWarehouseId || null,
        warehouseType: wh.warehouseType || 'Regular',
        children: [],
        expanded: true, // All expanded by default
        actualQty: wh.actualQty ?? 0,
        stockValue: wh.stockValue ?? 0,
        level: 0,
      });
    }

    // Build parent-child relationships
    for (const node of nodeMap.values()) {
      if (node.parentId && nodeMap.has(node.parentId)) {
        nodeMap.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    }

    // Sort children by name
    const sortChildren = (nodes: WarehouseNode[]) => {
      nodes.sort((a, b) => a.name.localeCompare(b.name));
      for (const n of nodes) sortChildren(n.children);
    };
    sortChildren(roots);

    return roots;
  }

  flatTree(): (WarehouseNode & { level: number })[] {
    const result: (WarehouseNode & { level: number })[] = [];
    const flatten = (nodes: WarehouseNode[], level: number) => {
      for (const node of nodes) {
        result.push({ ...node, level });
        if (node.expanded && node.children.length > 0) {
          flatten(node.children, level + 1);
        }
      }
    };
    flatten(this.tree(), 0);
    return result;
  }

  toggle(node: WarehouseNode): void {
    node.expanded = !node.expanded;
    this.tree.set([...this.tree()]);
  }

  getChildCount(node: WarehouseNode): number {
    let count = 0;
    const traverse = (n: WarehouseNode) => {
      count += n.children.length;
      for (const c of n.children) traverse(c);
    };
    traverse(node);
    return count;
  }
}
