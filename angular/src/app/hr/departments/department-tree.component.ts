import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { DepartmentService } from '../../proxy/human-resources/department.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { DepartmentDto } from '../../proxy/human-resources/models';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

export interface DepartmentNode {
  id: string;
  name: string;
  companyId: string;
  isGroup: boolean;
  parentId?: string | null;
  isActive: boolean;
  children: DepartmentNode[];
  level: number;
}

/**
 * Department master — hierarchical org unit per company.
 * Per ERPNext: Department (setup/doctype/department).
 */
@Component({
  selector: 'app-department-tree',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-sitemap me-2"></i>{{ '::Departments' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm(null)">
            <i class="fas fa-plus me-1"></i>{{ '::NewDepartment' | abpLocalization }}
          </button>
        </div>
        <div class="card-body">
          @if (showForm()) {
            <div class="border rounded p-3 mb-3 bg-light">
              <h6 class="mb-2">{{ editingId() ? ('::EditDepartment' | abpLocalization) : ('::NewDepartment' | abpLocalization) }}</h6>
              <div class="row g-2">
                <div class="col-md-3">
                  <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
                  <input class="form-control form-control-sm" [(ngModel)]="form.name" name="name" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Company' | abpLocalization }} *</label>
                  <select class="form-select form-select-sm" [(ngModel)]="form.companyId" name="companyId">
                    <option [ngValue]="''">—</option>
                    @for (c of companies(); track c.id) {
                      <option [ngValue]="c.id">{{ c.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::ParentDepartment' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" [(ngModel)]="form.parentId" name="parentId">
                    <option [ngValue]="null">— Root Level —</option>
                    @for (d of groupOptions(); track d.id) {
                      <option [ngValue]="d.id">{{ d.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-1">
                  <label class="form-label">{{ '::IsGroup' | abpLocalization }}</label>
                  <div class="form-check mt-1">
                    <input type="checkbox" class="form-check-input" [(ngModel)]="form.isGroup" name="isGroup" id="isGroupCheck" />
                  </div>
                </div>
                <div class="col-md-2 d-flex align-items-end gap-2">
                  <button class="btn btn-primary btn-sm" (click)="save()" [disabled]="!form.name || !form.companyId">
                    <i class="fas fa-save me-1"></i>{{ '::Save' | abpLocalization }}
                  </button>
                  <button class="btn btn-secondary btn-sm" (click)="cancelForm()">{{ '::Cancel' | abpLocalization }}</button>
                </div>
              </div>
            </div>
          }

          @if (loading()) {
            <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
          } @else if (tree().length === 0) {
            <div class="text-center text-muted py-5">
              <i class="fas fa-sitemap d-block mb-2" style="font-size: 2rem;"></i>
              <p>No departments defined yet.</p>
            </div>
          } @else {
            <div class="tree-view">
              @for (node of flattenedTree(); track node.id) {
                <div class="tree-node d-flex align-items-center py-1 border-bottom"
                  [style.padding-left.px]="node.level * 24 + 8">
                  @if (node.isGroup && node.children.length > 0) {
                    <button class="btn btn-sm btn-link p-0 me-1" (click)="toggle(node.id)">
                      <i class="fas" [class.fa-chevron-right]="!isExpanded(node.id)" [class.fa-chevron-down]="isExpanded(node.id)"></i>
                    </button>
                  } @else {
                    <span class="me-3" style="width: 16px;"></span>
                  }
                  @if (node.isGroup) {
                    <i class="fas fa-folder text-warning me-2"></i>
                  } @else {
                    <i class="fas fa-user-tag me-2 text-muted"></i>
                  }
                  <span class="flex-grow-1" [class.fw-medium]="node.isGroup">{{ node.name }}</span>
                  @if (!node.isActive) {
                    <span class="badge bg-secondary me-2">{{ '::Inactive' | abpLocalization }}</span>
                  }
                  <div class="btn-group btn-group-sm">
                    @if (node.isGroup) {
                      <button class="btn btn-outline-primary" title="Add child" (click)="addChildTo(node)"><i class="fas fa-plus"></i></button>
                    }
                    <button class="btn btn-outline-secondary" title="Edit" (click)="editNode(node)"><i class="fas fa-edit"></i></button>
                    <button class="btn btn-outline-danger" title="Delete" (click)="deleteNode(node.id)"><i class="fas fa-trash"></i></button>
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
    .tree-node:hover { background-color: rgba(0,0,0,0.03); }
    .tree-view { max-height: 70vh; overflow-y: auto; }
  `],
})
export class DepartmentTreeComponent implements OnInit {
  private service = inject(DepartmentService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  tree = signal<DepartmentNode[]>([]);
  companies = signal<{ id: string; name: string }[]>([]);
  groupOptions = signal<{ id: string; name: string }[]>([]);
  loading = signal(true);
  showForm = signal(false);
  editingId = signal<string | null>(null);
  expandedIds = new Set<string>();

  form: { name: string; companyId: string; parentId: string | null; isGroup: boolean; isActive: boolean } = {
    name: '', companyId: '', parentId: null, isGroup: false, isActive: true,
  };

  ngOnInit(): void {
    this.loadCompanies();
    this.loadData();
  }

  loadCompanies(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any).subscribe({
      next: res => this.companies.set((res.items ?? []).map((c: any) => ({ id: c.id, name: c.name }))),
      error: () => {},
    });
  }

  loadData(): void {
    this.loading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }).subscribe({
      next: result => {
        const items = (result.items ?? []) as DepartmentDto[];
        this.tree.set(this.buildTree(items));
        this.groupOptions.set(items.filter(i => i.isGroup).map(i => ({ id: (i as any).id!, name: i.name! })));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private buildTree(items: DepartmentDto[]): DepartmentNode[] {
    const map = new Map<string, DepartmentNode>();
    const roots: DepartmentNode[] = [];

    for (const item of items) {
      const id = (item as any).id as string;
      map.set(id, {
        id,
        name: item.name ?? '',
        companyId: item.companyId,
        isGroup: item.isGroup ?? false,
        parentId: item.parentId,
        isActive: item.isActive ?? true,
        children: [],
        level: 0,
      });
    }

    for (const item of items) {
      const id = (item as any).id as string;
      const node = map.get(id)!;
      if (item.parentId && map.has(item.parentId)) {
        map.get(item.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    }

    const setLevels = (nodes: DepartmentNode[], level: number) => {
      for (const n of nodes) {
        n.level = level;
        setLevels(n.children, level + 1);
      }
    };
    setLevels(roots, 0);

    return roots;
  }

  flattenedTree(): DepartmentNode[] {
    const result: DepartmentNode[] = [];
    const traverse = (nodes: DepartmentNode[]) => {
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

  toggle(id: string): void {
    if (this.expandedIds.has(id)) this.expandedIds.delete(id);
    else this.expandedIds.add(id);
  }

  isExpanded(id: string): boolean {
    return this.expandedIds.has(id);
  }

  openForm(parentId: string | null): void {
    this.editingId.set(null);
    this.form = { name: '', companyId: this.companies()[0]?.id ?? '', parentId, isGroup: false, isActive: true };
    this.showForm.set(true);
  }

  addChildTo(parent: DepartmentNode): void {
    this.editingId.set(null);
    this.form = { name: '', companyId: parent.companyId, parentId: parent.id, isGroup: false, isActive: true };
    this.showForm.set(true);
    this.expandedIds.add(parent.id);
  }

  editNode(node: DepartmentNode): void {
    this.editingId.set(node.id);
    this.form = { name: node.name, companyId: node.companyId, parentId: node.parentId ?? null, isGroup: node.isGroup, isActive: node.isActive };
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
  }

  save(): void {
    if (!this.form.name || !this.form.companyId) return;

    const payload = {
      name: this.form.name,
      companyId: this.form.companyId,
      parentId: this.form.parentId || null,
      isGroup: this.form.isGroup,
      isActive: this.form.isActive,
    };

    const request$ = this.editingId()
      ? this.service.update(this.editingId()!, payload as any)
      : this.service.create(payload as any);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.cancelForm();
        this.loadData();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  deleteNode(id: string): void {
    this.service.delete(id).subscribe({
      next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadData(); },
      error: () => this.toaster.error('::OperationFailed'),
    });
  }
}
