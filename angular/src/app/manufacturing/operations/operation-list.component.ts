import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { OperationDto } from '../../proxy/manufacturing/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-operation-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'Operations' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/manufacturing/operations/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewOperation' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <div class="text-center py-3"><i class="fa fa-spinner fa-spin fa-2x"></i></div> }

      @if (!isLoading && operations.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-wrench fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoOperationsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'WorkstationType' | abpLocalization }}</th>
              <th class="text-end">{{ 'TotalOperationTime' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (op of operations; track op.id) {
                <tr>
                  <td><a [routerLink]="['/manufacturing/operations', op.id]">{{ op.name }}</a></td>
                  <td>{{ op.workstationType ?? '—' }}</td>
                  <td class="text-end">{{ op.totalOperationTime | number:'1.0-2' }}</td>
                  <td><span class="badge" [class]="op.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ (op.isActive ? 'Active' : 'Inactive') | abpLocalization }}
                  </span></td>
                  <td class="text-end">
                    <button class="btn btn-sm btn-outline-danger" (click)="delete(op)"><i class="fa fa-trash"></i></button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `,
})
export class OperationListComponent implements OnInit {
  private service = inject(ManufacturingService);
  private confirmation = inject(ConfirmationService);
  operations: OperationDto[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit(): void { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.service.getOperationList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, sorting: '' })
      .subscribe({
        next: (r) => { this.operations = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; },
        error: () => { this.isLoading = false; },
      });
  }

  delete(op: OperationDto) {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm && op.id) {
        this.service.deleteOperation(op.id).subscribe(() => this.loadData());
      }
    });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
