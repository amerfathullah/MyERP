import { Component, inject, OnInit } from '@angular/core';
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
  template: `
    <abp-page [title]="'QualityProcedures' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <div class="d-flex gap-2">
            <input
              type="text"
              class="form-control form-control-sm"
              style="max-width: 250px"
              [placeholder]="'::Placeholder:Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <a routerLink="/inventory/quality-procedures/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ 'NewQualityProcedure' | abpLocalization }}
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
        <div class="card-footer d-flex justify-content-between align-items-center">
          <span class="text-muted small">Total: {{ totalCount }}</span>
          <app-pagination
            [totalCount]="totalCount"
            [currentPage]="currentPage"
            [pageSize]="pageSize"
            (pageChange)="onPageChange($event)"
          ></app-pagination>
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityProcedureListComponent implements OnInit {
  private readonly service = inject(QualityManagementService);
  private readonly confirmation = inject(ConfirmationService);

  items: QualityProcedureDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 10;
  searchTerm = '';

  ngOnInit() {
    this.loadData();
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
        this.service.deleteProcedure(id).subscribe(() => this.loadData());
      }
    });
  }
}
