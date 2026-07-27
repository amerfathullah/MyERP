import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemGroupService } from '../../proxy/inventory/item-group.service';
import type { ItemGroupDto } from '../../proxy/inventory/models';
import { ToasterService } from '@abp/ng.theme.shared';

export interface ItemGroupNode {
  id: string;
  name: string;
  isGroup: boolean;
  parentId?: string | null;
  defaultWarehouseId?: string | null;
  children: ItemGroupNode[];
  level: number;
}

@Component({
  selector: 'app-item-group-tree',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="bi bi-diagram-3 me-2"></i>{{ 'MyERP::ItemGroups' | abpLocalization }}</h5>
        <button class="btn btn-primary btn-sm" (click)="showCreateForm = true; newGroup.parentId = null">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewItemGroup' | abpLocalization }}
        </button>
      </div>
      <div class="card-body">
        <!-- Create/Edit Form -->
        @if (showCreateForm) {
          <div class="border rounded p-3 mb-3 bg-light">
            <h6 class="mb-2">{{ newGroup.id ? 'Edit' : 'New' }} Item Group</h6>
            <div class="row g-2">
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::Name' | abpLocalization }} *</label>
                <input class="form-control form-control-sm" [(ngModel)]="newGroup.name" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'MyERP::ParentGroup' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newGroup.parentId">
                  <option [ngValue]="null">— Root Level —</option>
                  @for (g of groupOptions(); track g.id) {
                    <option [value]="g.id">{{ g.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ 'MyERP::IsGroup' | abpLocalization }}</label>
                <div class="form-check mt-1">
                  <input type="checkbox" class="form-check-input" [(ngModel)]="newGroup.isGroup" id="isGroupCheck" />
                  <label class="form-check-label" for="isGroupCheck">Group</label>
                </div>
              </div>
              <div class="col-md-3 d-flex align-items-end gap-2">
                <button class="btn btn-primary btn-sm" (click)="saveGroup()" [disabled]="!newGroup.name">
                  <i class="bi bi-check-lg me-1"></i>{{ 'MyERP::Save' | abpLocalization }}
                </button>
                <button class="btn btn-secondary btn-sm" (click)="cancelForm()">{{ 'MyERP::Cancel' | abpLocalization }}</button>
              </div>
            </div>
          </div>
        }

        <!-- Tree View -->
        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else if (tree().length === 0) {
          <div class="text-center text-muted py-5">
            <i class="bi bi-diagram-3 d-block mb-2" style="font-size: 2rem;"></i>
            <p>No item groups defined yet. Create your first group to organize items.</p>
          </div>
        } @else {
          <div class="tree-view">
            @for (node of flattenedTree(); track node.id) {
              <div class="tree-node d-flex align-items-center py-1 border-bottom"
                [style.padding-left.px]="node.level * 24 + 8">
                <!-- Expand/Collapse toggle -->
                @if (node.isGroup && node.children.length > 0) {
                  <button class="btn btn-sm btn-link p-0 me-1" (click)="toggle(node.id)">
                    <i class="bi" [class.bi-chevron-right]="!isExpanded(node.id)"
                      [class.bi-chevron-down]="isExpanded(node.id)"></i>
                  </button>
                } @else {
                  <span class="me-3" style="width: 16px;"></span>
                }

                <!-- Icon -->
                @if (node.isGroup) {
                  <i class="bi bi-folder-fill text-warning me-2"></i>
                } @else {
                  <i class="bi bi-tag me-2 text-muted"></i>
                }

                <!-- Name -->
                <span class="flex-grow-1" [class.fw-medium]="node.isGroup">{{ node.name }}</span>

                <!-- Actions -->
                <div class="btn-group btn-group-sm">
                  @if (node.isGroup) {
                    <button class="btn btn-outline-primary" title="Add child"
                      (click)="addChildTo(node)">
                      <i class="bi bi-plus"></i>
                    </button>
                  }
                  <button class="btn btn-outline-secondary" title="Edit"
                    (click)="editGroup(node)">
                    <i class="bi bi-pencil"></i>
                  </button>
                </div>
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .tree-node:hover { background-color: #f8f9fa; }
    .tree-view { max-height: 70vh; overflow-y: auto; }
  `],
})
export class ItemGroupTreeComponent implements OnInit {
  private service = inject(ItemGroupService);
  private toaster = inject(ToasterService);

  tree = signal<ItemGroupNode[]>([]);
  groupOptions = signal<{ id: string; name: string }[]>([]);
  loading = signal(true);
  showCreateForm = false;
  expandedIds = new Set<string>();

  newGroup: { id?: string; name: string; parentId: string | null; isGroup: boolean } = {
    name: '', parentId: null, isGroup: false,
  };

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }).subscribe({
      next: (result) => {
        const items = result.items ?? [];
        this.tree.set(this.buildTree(items));
        this.groupOptions.set(items.filter(i => i.isGroup).map(i => ({ id: i.id!, name: i.name! })));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private buildTree(items: ItemGroupDto[]): ItemGroupNode[] {
    const map = new Map<string, ItemGroupNode>();
    const roots: ItemGroupNode[] = [];

    for (const item of items) {
      map.set(item.id!, {
        id: item.id!,
        name: item.name ?? '',
        isGroup: item.isGroup ?? false,
        parentId: item.parentId,
        defaultWarehouseId: item.defaultWarehouseId,
        children: [],
        level: 0,
      });
    }

    for (const item of items) {
      const node = map.get(item.id!)!;
      if (item.parentId && map.has(item.parentId)) {
        const parent = map.get(item.parentId)!;
        node.level = parent.level + 1;
        parent.children.push(node);
      } else {
        roots.push(node);
      }
    }

    // Calculate correct levels via BFS
    const setLevels = (nodes: ItemGroupNode[], level: number) => {
      for (const n of nodes) {
        n.level = level;
        setLevels(n.children, level + 1);
      }
    };
    setLevels(roots, 0);

    return roots;
  }

  flattenedTree(): ItemGroupNode[] {
    const result: ItemGroupNode[] = [];
    const traverse = (nodes: ItemGroupNode[]) => {
      for (const node of nodes) {
        result.push(node);
        if (node.isGroup && this.isExpanded(node.id)) {
          traverse(node.children);
        }
      }
    };
    traverse(this.tree());
    return result;
  }

  toggle(id: string) {
    if (this.expandedIds.has(id)) this.expandedIds.delete(id);
    else this.expandedIds.add(id);
  }

  isExpanded(id: string): boolean {
    return this.expandedIds.has(id);
  }

  addChildTo(parent: ItemGroupNode) {
    this.newGroup = { name: '', parentId: parent.id, isGroup: false };
    this.showCreateForm = true;
    // Expand parent to show new child
    this.expandedIds.add(parent.id);
  }

  editGroup(node: ItemGroupNode) {
    this.newGroup = { id: node.id, name: node.name, parentId: node.parentId ?? null, isGroup: node.isGroup };
    this.showCreateForm = true;
  }

  cancelForm() {
    this.showCreateForm = false;
    this.newGroup = { name: '', parentId: null, isGroup: false };
  }

  saveGroup() {
    if (!this.newGroup.name) return;

    const payload = {
      name: this.newGroup.name,
      parentId: this.newGroup.parentId || undefined,
      isGroup: this.newGroup.isGroup,
    };

    // Create only (update would need a separate endpoint)
    this.service.create(payload as any).subscribe({
      next: () => {
        this.toaster.success('MyERP::SuccessfullySaved');
        this.cancelForm();
        this.loadData();
      },
      error: (err) => {
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      },
    });
  }
}
