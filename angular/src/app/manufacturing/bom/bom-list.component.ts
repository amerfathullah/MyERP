import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SortableHeaderComponent, type SortEvent } from '../../shared/components/sortable-header/sortable-header.component';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { BomDto } from '../../proxy/manufacturing/models';

@Component({
  selector: 'app-bom-list',
  standalone: true,
  imports: [PaginationComponent, SortableHeaderComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'BillOfMaterials' | abpLocalization">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <span class="text-muted small">{{ totalCount }} {{ '::Results' | abpLocalization }}</span>
        <a routerLink="/manufacturing/bom/new" class="btn btn-primary btn-sm">
          <i class="fa fa-plus me-1"></i>{{ 'NewBOM' | abpLocalization }}
        </a>
      </div>

      <div class="card">
        <div class="card-body">
          <div class="row mb-3 align-items-center">
            <div class="col-auto">
              <div class="input-group input-group-sm" style="width: 250px;">
                <span class="input-group-text"><i class="fa fa-search"></i></span>
                <input type="text" class="form-control"
                  [placeholder]="'::Search' | abpLocalization"
                  [(ngModel)]="searchTerm" (ngModelChange)="onSearch($event)" />
              </div>
            </div>
          </div>

          @if (isLoading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (boms().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-sitemap fa-3x mb-3 d-block"></i>
              <h6 class="text-muted">{{ '::NoRecordsFound' | abpLocalization }}</h6>
              <p class="text-muted small mb-3">{{ 'NoBOMsYet' | abpLocalization }}</p>
              <a routerLink="/manufacturing/bom/new" class="btn btn-primary btn-sm"><i class="fa fa-plus me-1"></i>{{ 'NewBOM' | abpLocalization }}</a>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover w-100">
                <thead>
                  <tr>
                    <th style="min-width: 140px;">
                      <app-sortable-header field="bomNumber" [currentField]="sortField" [currentDirection]="sortDirection" (sort)="onSort($event)">
                        {{ 'BOMNumber' | abpLocalization }}
                      </app-sortable-header>
                    </th>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th class="text-end" style="width: 100px;">{{ 'Quantity' | abpLocalization }}</th>
                    <th class="text-end" style="width: 120px;">
                      <app-sortable-header field="totalCost" [currentField]="sortField" [currentDirection]="sortDirection" (sort)="onSort($event)">
                        {{ 'TotalCost' | abpLocalization }}
                      </app-sortable-header>
                    </th>
                    <th class="text-center" style="width: 110px;">{{ 'Status' | abpLocalization }}</th>
                    <th class="text-end" style="width: 80px;"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (bom of boms(); track bom.id) {
                    <tr>
                      <td class="fw-semibold">
                        <a [routerLink]="['/manufacturing/bom', bom.id]" class="text-primary text-decoration-none">{{ bom.bomNumber ?? '—' }}</a>
                      </td>
                      <td>
                        <a [routerLink]="['/inventory/items', bom.itemId]" class="text-decoration-none">{{ bom.itemName ?? bom.itemId }}</a>
                      </td>
                      <td class="text-end font-monospace">{{ bom.quantity | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ bom.totalCost | number:'1.2-2' }}</td>
                      <td class="text-center">
                        @if (bom.isDefault) { <span class="badge bg-primary">{{ '::Default' | abpLocalization }}</span> }
                        @else if (bom.isActive) { <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span> }
                        @else { <span class="badge bg-secondary">{{ '::Inactive' | abpLocalization }}</span> }
                      </td>
                      <td class="text-end">
                        <a [routerLink]="['/manufacturing/bom', bom.id]" class="btn btn-sm btn-outline-primary"><i class="fa fa-eye"></i></a>
                        @if (!bom.isDefault) {
                          <button class="btn btn-sm btn-outline-danger ms-1" (click)="deleteBom(bom)"><i class="fa fa-trash"></i></button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class BomListComponent implements OnInit {
  private manufacturingService = inject(ManufacturingService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  boms = signal<BomDto[]>([]);
  isLoading = signal(true);
  searchTerm = '';
  totalCount = 0;
  currentPage = 0;
  pageSize = 20;
  sortField: string | null = 'bomNumber';
  sortDirection: 'asc' | 'desc' = 'asc';

  ngOnInit(): void { this.loadData(); }

  loadData() {
    this.isLoading.set(true);
    this.manufacturingService.getBomList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: this.sortField ? `${this.sortField} ${this.sortDirection}` : '',
      filter: this.searchTerm || undefined,
    } as any).subscribe({
      next: res => { this.boms.set(res.items ?? []); this.totalCount = res.totalCount ?? 0; this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  onSearch(term: string): void { this.searchTerm = term; this.currentPage = 0; this.loadData(); }

  onSort(event: SortEvent): void {
    this.sortField = event.field;
    this.sortDirection = event.direction;
    this.currentPage = 0;
    this.loadData();
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }

  deleteBom(bom: BomDto): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.manufacturingService.deleteBom(bom.id!).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadData(); },
        error: (err) => {
          const reason = err?.error?.error?.data?.reason || err?.error?.error?.message;
          this.toaster.error(reason || '::OperationFailed');
        },
      });
    });
  }
}
