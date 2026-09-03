import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import type { CostCenterDto } from '../../proxy/accounting/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';

interface CcNode {
  id: string;
  name: string;
  costCenterNumber?: string | null;
  isGroup: boolean;
  parentId?: string | null;
  isActive: boolean;
  children: CcNode[];
  level: number;
}

@Component({
  selector: 'app-cost-center-tree',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="bi bi-bullseye me-2"></i>{{ 'MyERP::CostCenters' | abpLocalization }}</h5>
        <button class="btn btn-primary btn-sm" (click)="showForm = true; resetForm()">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewCostCenter' | abpLocalization }}
        </button>
      </div>
      <div class="card-body">
        @if (showForm) {
          <div class="border rounded p-3 mb-3 bg-light">
            <div class="row g-2">
              <div class="col-md-4">
                <label class="form-label">Name *</label>
                <input class="form-control form-control-sm" [(ngModel)]="formName" />
              </div>
              <div class="col-md-3">
                <label class="form-label">Parent</label>
                <select class="form-select form-select-sm" [(ngModel)]="formParentId">
                  <option [ngValue]="null">— Root —</option>
                  @for (g of groups(); track g.id) {
                    <option [value]="g.id">{{ g.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-2">
                <label class="form-label">Group</label>
                <div class="form-check mt-1">
                  <input type="checkbox" class="form-check-input" [(ngModel)]="formIsGroup" id="ccGrp" />
                  <label class="form-check-label" for="ccGrp">Yes</label>
                </div>
              </div>
              <div class="col-md-3 d-flex align-items-end gap-2">
                <button class="btn btn-primary btn-sm" (click)="save()" [disabled]="!formName">Save</button>
                <button class="btn btn-secondary btn-sm" (click)="showForm = false">Cancel</button>
              </div>
            </div>
          </div>
        }

        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <div style="max-height: 60vh; overflow-y: auto;">
            @for (node of flatTree(); track node.id) {
              <div class="d-flex align-items-center py-1 border-bottom"
                [style.padding-left.px]="node.level * 22 + 8">
                @if (node.isGroup && node.children.length > 0) {
                  <button class="btn btn-sm btn-link p-0 me-1" (click)="toggle(node.id)">
                    <i class="bi" [class.bi-chevron-right]="!expanded.has(node.id)"
                      [class.bi-chevron-down]="expanded.has(node.id)"></i>
                  </button>
                } @else {
                  <span style="width: 20px;" class="me-1"></span>
                }
                @if (node.isGroup) {
                  <i class="bi bi-folder-fill text-primary me-2"></i>
                } @else {
                  <i class="bi bi-bullseye text-muted me-2"></i>
                }
                <span class="flex-grow-1 d-flex align-items-center" [class.fw-medium]="node.isGroup">
                  <span [class.text-muted]="!node.isActive">{{ node.name }}</span>
                  @if (node.costCenterNumber) {
                    <span class="badge bg-light text-dark border ms-2">{{ node.costCenterNumber }}</span>
                  }
                  @if (!node.isActive) {
                    <span class="badge bg-secondary ms-2">Disabled</span>
                  }
                </span>
                @if (node.isGroup) {
                  <button class="btn btn-sm btn-outline-primary" (click)="addChildTo(node)">
                    <i class="bi bi-plus"></i>
                  </button>
                }
              </div>
            } @empty {
              <div class="text-center text-muted py-4">No cost centers defined.</div>
            }
          </div>
        }
      </div>
    </div>
  `,
})
export class CostCenterTreeComponent implements OnInit {
  private service = inject(CostCenterService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  tree = signal<CcNode[]>([]);
  groups = signal<{ id: string; name: string }[]>([]);
  loading = signal(true);
  showForm = false;
  expanded = new Set<string>();

  formName = '';
  formParentId: string | null = null;
  formIsGroup = false;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.loading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 500, sorting: '', companyId: this.companyContext.currentCompanyId() || undefined } as any).subscribe({
      next: (res) => {
        const items = res.items ?? [];
        this.tree.set(this.buildTree(items));
        this.groups.set(items.filter(i => i.isGroup).map(i => ({ id: i.id!, name: i.name! })));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private buildTree(items: CostCenterDto[]): CcNode[] {
    const map = new Map<string, CcNode>();
    const roots: CcNode[] = [];
    for (const i of items) map.set(i.id!, { id: i.id!, name: i.name ?? '', costCenterNumber: i.costCenterNumber, isGroup: i.isGroup ?? false, parentId: i.parentId, isActive: i.isActive ?? true, children: [], level: 0 });
    for (const i of items) { const n = map.get(i.id!)!; if (i.parentId && map.has(i.parentId)) map.get(i.parentId)!.children.push(n); else roots.push(n); }
    const setLvl = (ns: CcNode[], l: number) => { for (const n of ns) { n.level = l; setLvl(n.children, l + 1); } };
    setLvl(roots, 0);
    return roots;
  }

  flatTree(): CcNode[] {
    const r: CcNode[] = [];
    const t = (ns: CcNode[]) => { for (const n of ns) { r.push(n); if (this.expanded.has(n.id)) t(n.children); } };
    t(this.tree());
    return r;
  }

  toggle(id: string) { this.expanded.has(id) ? this.expanded.delete(id) : this.expanded.add(id); }
  addChildTo(node: CcNode) { this.formName = ''; this.formParentId = node.id; this.formIsGroup = false; this.showForm = true; this.expanded.add(node.id); }
  resetForm() { this.formName = ''; this.formParentId = null; this.formIsGroup = false; }

  save() {
    if (!this.formName) return;
    this.service.create({ name: this.formName, parentId: this.formParentId || undefined, isGroup: this.formIsGroup, companyId: this.companyContext.currentCompanyId() } as any).subscribe({
      next: () => { this.toaster.success('MyERP::SuccessfullySaved'); this.showForm = false; this.loadData(); },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
