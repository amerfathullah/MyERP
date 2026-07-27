import { Component, inject, OnInit, signal, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HierarchyMasterDataService } from '../../../proxy/core/hierarchy-master-data.service';
import type { HierarchyNodeDto, CreateHierarchyNodeDto } from '../../../proxy/core/models';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';

interface TreeNode {
  id: string;
  name: string;
  parentId?: string | null;
  isGroup: boolean;
  children: TreeNode[];
  level: number;
}

type HierarchyType = 'CustomerGroup' | 'SupplierGroup' | 'Territory';

@Component({
  selector: 'app-hierarchy-tree',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i [class]="iconClass + ' me-2'"></i>{{ title }}</h5>
        <button class="btn btn-primary btn-sm" (click)="showForm = true; resetForm()">
          <i class="bi bi-plus-lg me-1"></i>Add New
        </button>
      </div>
      <div class="card-body">
        @if (showForm) {
          <div class="border rounded p-3 mb-3 bg-light">
            <div class="row g-2">
              <div class="col-md-4">
                <label class="form-label">Name *</label>
                <input class="form-control form-control-sm" [(ngModel)]="formData.name" />
              </div>
              <div class="col-md-3">
                <label class="form-label">Parent</label>
                <select class="form-select form-select-sm" [(ngModel)]="formData.parentId">
                  <option [ngValue]="null">— Root Level —</option>
                  @for (g of groupNodes(); track g.id) {
                    <option [value]="g.id">{{ g.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-2">
                <label class="form-label">Type</label>
                <div class="form-check mt-1">
                  <input type="checkbox" class="form-check-input" [(ngModel)]="formData.isGroup" id="grpCheck" />
                  <label class="form-check-label" for="grpCheck">Group</label>
                </div>
              </div>
              <div class="col-md-3 d-flex align-items-end gap-2">
                <button class="btn btn-primary btn-sm" (click)="save()" [disabled]="!formData.name">
                  <i class="bi bi-check-lg me-1"></i>Save
                </button>
                <button class="btn btn-secondary btn-sm" (click)="showForm = false">Cancel</button>
              </div>
            </div>
          </div>
        }

        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else if (tree().length === 0) {
          <div class="text-center text-muted py-5">
            <i [class]="iconClass + ' d-block mb-2'" style="font-size: 2rem;"></i>
            <p>No {{ title.toLowerCase() }} defined yet.</p>
          </div>
        } @else {
          <div style="max-height: 60vh; overflow-y: auto;">
            @for (node of flatTree(); track node.id) {
              <div class="d-flex align-items-center py-1 border-bottom"
                [style.padding-left.px]="node.level * 22 + 8"
                style="cursor: default;">
                @if (node.isGroup && node.children.length > 0) {
                  <button class="btn btn-sm btn-link p-0 me-1" (click)="toggle(node.id)">
                    <i class="bi" [class.bi-chevron-right]="!expanded.has(node.id)"
                      [class.bi-chevron-down]="expanded.has(node.id)"></i>
                  </button>
                } @else {
                  <span style="width: 20px;" class="me-1"></span>
                }
                @if (node.isGroup) {
                  <i class="bi bi-folder-fill text-warning me-2"></i>
                } @else {
                  <i class="bi bi-dot text-muted me-1"></i>
                }
                <span class="flex-grow-1" [class.fw-medium]="node.isGroup">{{ node.name }}</span>
                <div class="btn-group btn-group-sm opacity-75">
                  @if (node.isGroup) {
                    <button class="btn btn-outline-primary" (click)="addChildTo(node)" title="Add child">
                      <i class="bi bi-plus"></i>
                    </button>
                  }
                  <button class="btn btn-outline-danger" (click)="deleteNode(node)" title="Delete">
                    <i class="bi bi-trash"></i>
                  </button>
                </div>
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
  styles: [`.d-flex:hover { background-color: #f8f9fa; }`],
})
export class HierarchyTreeComponent implements OnInit {
  @Input() type: HierarchyType = 'CustomerGroup';
  @Input() title = 'Hierarchy';
  @Input() iconClass = 'bi bi-diagram-3';

  private service = inject(HierarchyMasterDataService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  tree = signal<TreeNode[]>([]);
  groupNodes = signal<{ id: string; name: string }[]>([]);
  loading = signal(true);
  showForm = false;
  expanded = new Set<string>();
  formData: CreateHierarchyNodeDto = { name: '', parentId: null, isGroup: false };

  ngOnInit() { this.loadData(); }

  loadData() {
    this.loading.set(true);
    const obs = this.type === 'CustomerGroup' ? this.service.getCustomerGroups()
      : this.type === 'SupplierGroup' ? this.service.getSupplierGroups()
      : this.service.getTerritories();

    obs.subscribe({
      next: (items) => {
        this.tree.set(this.buildTree(items));
        this.groupNodes.set((items ?? []).filter(i => i.isGroup).map(i => ({ id: i.id!, name: i.name! })));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private buildTree(items: HierarchyNodeDto[]): TreeNode[] {
    const map = new Map<string, TreeNode>();
    const roots: TreeNode[] = [];
    for (const item of items ?? []) {
      map.set(item.id!, { id: item.id!, name: item.name ?? '', parentId: item.parentId, isGroup: item.isGroup ?? false, children: [], level: 0 });
    }
    for (const item of items ?? []) {
      const node = map.get(item.id!)!;
      if (item.parentId && map.has(item.parentId)) {
        map.get(item.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    }
    const setLevels = (nodes: TreeNode[], lvl: number) => { for (const n of nodes) { n.level = lvl; setLevels(n.children, lvl + 1); } };
    setLevels(roots, 0);
    return roots;
  }

  flatTree(): TreeNode[] {
    const result: TreeNode[] = [];
    const traverse = (nodes: TreeNode[]) => {
      for (const n of nodes) { result.push(n); if (this.expanded.has(n.id)) traverse(n.children); }
    };
    traverse(this.tree());
    return result;
  }

  toggle(id: string) { this.expanded.has(id) ? this.expanded.delete(id) : this.expanded.add(id); }

  addChildTo(node: TreeNode) { this.formData = { name: '', parentId: node.id, isGroup: false }; this.showForm = true; this.expanded.add(node.id); }

  resetForm() { this.formData = { name: '', parentId: null, isGroup: false }; }

  save() {
    if (!this.formData.name) return;
    const obs = this.type === 'CustomerGroup' ? this.service.createCustomerGroup(this.formData)
      : this.type === 'SupplierGroup' ? this.service.createSupplierGroup(this.formData)
      : this.service.createTerritory(this.formData);

    obs.subscribe({
      next: () => { this.toaster.success('MyERP::SuccessfullySaved'); this.showForm = false; this.loadData(); },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }

  deleteNode(node: TreeNode) {
    this.confirmation.warn(`Delete "${node.name}"?`, 'MyERP::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      const obs = this.type === 'CustomerGroup' ? this.service.deleteCustomerGroup(node.id)
        : this.type === 'SupplierGroup' ? this.service.deleteSupplierGroup(node.id)
        : this.service.deleteTerritory(node.id);
      obs.subscribe({
        next: () => { this.toaster.success('Deleted'); this.loadData(); },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Delete failed'),
      });
    });
  }
}
