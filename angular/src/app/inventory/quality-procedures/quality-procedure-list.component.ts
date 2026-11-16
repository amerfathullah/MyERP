import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { QualityProcedureDto } from '../../proxy/inventory/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-quality-procedure-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::QualityProcedures' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div class="d-flex gap-2 align-items-center">
            <input
              type="text"
              class="form-control form-control-sm"
              style="max-width: 250px"
              [placeholder]="'::Placeholder:Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
            <div class="btn-group btn-group-sm">
              <button type="button" class="btn"
                [class.btn-primary]="viewMode === 'list'"
                [class.btn-outline-secondary]="viewMode !== 'list'"
                (click)="setViewMode('list')">
                <i class="fa fa-list me-1"></i>{{ '::ListView' | abpLocalization }}
              </button>
              <button type="button" class="btn"
                [class.btn-primary]="viewMode === 'tree'"
                [class.btn-outline-secondary]="viewMode !== 'tree'"
                (click)="setViewMode('tree')">
                <i class="fa fa-sitemap me-1"></i>{{ '::TreeView' | abpLocalization }}
              </button>
            </div>
          </div>
          <a routerLink="/inventory/quality-procedures/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ '::NewQualityProcedure' | abpLocalization }}
          </a>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table class="table table-hover table-striped mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::ProcessOwner' | abpLocalization }}</th>
                  <th>{{ '::IsGroup' | abpLocalization }}</th>
                  <th>{{ '::StepsCount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (isLoading) {
                  <tr>
                    <td colspan="5" class="text-center py-4">
                      <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
                    </td>
                  </tr>
                } @else if (viewMode === 'tree') {
                  @if (filteredTreeNodes().length === 0) {
                    <tr>
                      <td colspan="5" class="text-center py-4 text-muted">
                        {{ '::NoData' | abpLocalization }}
                      </td>
                    </tr>
                  } @else {
                    @for (row of filteredTreeNodes(); track row.node.id) {
                      <tr>
                        <td class="fw-semibold" [style.paddingLeft.px]="12 + row.depth * 24">
                          @if (row.depth > 0) {
                            <span class="text-muted me-1">└─</span>
                          }
                          <i class="fa me-1" [ngClass]="row.node.isGroup ? 'fa-folder text-warning' : 'fa-file-text text-primary'"></i>
                          <a [routerLink]="['/inventory/quality-procedures', row.node.id]">{{ row.node.name }}</a>
                        </td>
                        <td>{{ row.node.processOwner ?? '-' }}</td>
                        <td>
                          <span class="badge" [ngClass]="row.node.isGroup ? 'bg-info' : 'bg-light text-dark'">
                            {{ row.node.isGroup ? 'Group' : 'Leaf' }}
                          </span>
                        </td>
                        <td>{{ row.node.steps?.length ?? 0 }}</td>
                        <td class="text-end">
                          <a [routerLink]="['/inventory/quality-procedures', row.node.id]" class="btn btn-outline-secondary btn-sm me-1">
                            <i class="fa fa-pencil"></i>
                          </a>
                          <button class="btn btn-outline-danger btn-sm" (click)="deleteProcedure(row.node.id, row.node.name)">
                            <i class="fa fa-trash"></i>
                          </button>
                        </td>
                      </tr>
                    }
                  }
                } @else if (filteredItems().length === 0) {
                  <tr>
                    <td colspan="5" class="text-center py-4 text-muted">
                      {{ '::NoData' | abpLocalization }}
                    </td>
                  </tr>
                } @else {
                  @for (proc of filteredItems(); track proc.id) {
                    <tr>
                      <td class="fw-semibold">
                        <i class="fa me-1" [ngClass]="proc.isGroup ? 'fa-folder text-warning' : 'fa-file-text text-primary'"></i>
                        <a [routerLink]="['/inventory/quality-procedures', proc.id]">{{ proc.name }}</a>
                      </td>
                      <td>{{ proc.processOwner ?? '-' }}</td>
                      <td>
                        <span class="badge" [ngClass]="proc.isGroup ? 'bg-info' : 'bg-light text-dark'">
                          {{ proc.isGroup ? 'Group' : 'Leaf' }}
                        </span>
                      </td>
                      <td>{{ proc.steps?.length ?? 0 }}</td>
                      <td class="text-end">
                        <a [routerLink]="['/inventory/quality-procedures', proc.id]" class="btn btn-outline-secondary btn-sm me-1">
                          <i class="fa fa-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger btn-sm" (click)="deleteProcedure(proc.id, proc.name)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        </div>
        @if (viewMode === 'list') {
          <div class="card-footer d-flex justify-content-between align-items-center">
            <span class="text-muted small">Total: {{ totalCount }}</span>
            <app-pagination
              [totalCount]="totalCount"
              [currentPage]="currentPage"
              [pageSize]="pageSize"
              (pageChange)="onPageChange($event)"
            ></app-pagination>
          </div>
        }
      </div>
    </abp-page>
  `,
})
export class QualityProcedureListComponent implements OnInit {
  private readonly service = inject(QualityManagementService);
  private readonly confirmation = inject(ConfirmationService);

  items: QualityProcedureDto[] = [];
  treeNodes: { node: QualityProcedureDto; depth: number }[] = [];
  viewMode: 'list' | 'tree' = 'list';
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 10;
  searchTerm = '';

  ngOnInit() {
    this.loadData();
  }

  setViewMode(mode: 'list' | 'tree') {
    this.viewMode = mode;
    if (mode === 'tree') {
      this.loadTree();
    } else {
      this.loadData();
    }
  }

  loadTree() {
    this.isLoading = true;
    this.service.getProcedureTree().subscribe({
      next: (res) => {
        this.treeNodes = this.buildTreeRows(res ?? []);
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  buildTreeRows(procs: QualityProcedureDto[]): { node: QualityProcedureDto; depth: number }[] {
    const idSet = new Set(procs.map(p => p.id).filter((id): id is string => !!id));
    const childrenMap = new Map<string, QualityProcedureDto[]>();
    const roots: QualityProcedureDto[] = [];

    for (const p of procs) {
      const parentId = p.parentQualityProcedureId;
      if (parentId && idSet.has(parentId)) {
        const list = childrenMap.get(parentId) ?? [];
        list.push(p);
        childrenMap.set(parentId, list);
      } else {
        roots.push(p);
      }
    }

    const result: { node: QualityProcedureDto; depth: number }[] = [];
    const visited = new Set<string>();
    const visit = (node: QualityProcedureDto, depth: number) => {
      if (node.id && visited.has(node.id)) return;
      if (node.id) visited.add(node.id);
      result.push({ node, depth });
      if (node.id) {
        for (const child of childrenMap.get(node.id) ?? []) {
          visit(child, depth + 1);
        }
      }
    };

    for (const r of roots) {
      visit(r, 0);
    }
    return result;
  }

  filteredTreeNodes() {
    const term = this.searchTerm.toLowerCase().trim();
    if (!term) return this.treeNodes;
    return this.treeNodes.filter(r => (r.node.name ?? '').toLowerCase().includes(term));
  }

  loadData() {
    this.isLoading = true;
    this.service.getProcedureList({
      maxResultCount: this.pageSize,
      skipCount: this.currentPage * this.pageSize,
    }).subscribe({
      next: (res) => {
        this.items = res.items ?? [];
        this.totalCount = res.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  filteredItems() {
    const term = this.searchTerm.toLowerCase().trim();
    if (!term) return this.items;
    return this.items.filter(i => (i.name ?? '').toLowerCase().includes(term));
  }

  deleteProcedure(id: string, name?: string) {
    this.confirmation.warn('::AreYouSureToDelete', name ?? 'Procedure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.deleteProcedure(id).subscribe(() => {
          if (this.viewMode === 'tree') {
            this.loadTree();
          } else {
            this.loadData();
          }
        });
      }
    });
  }
}
