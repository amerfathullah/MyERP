import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { RoutingDto } from '../../proxy/manufacturing/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-routing-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'Routings' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/manufacturing/routings/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewRouting' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <div class="text-center py-3"><i class="fa fa-spinner fa-spin fa-2x"></i></div> }

      @if (!isLoading && routings.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-diagram-project fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoRoutingsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'Operations' | abpLocalization }}</th>
              <th class="text-end">{{ 'OperatingCost' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (r of routings; track r.id) {
                <tr>
                  <td><a [routerLink]="['/manufacturing/routings', r.id]">{{ r.name }}</a></td>
                  <td>{{ r.operations?.length ?? 0 }}</td>
                  <td class="text-end">{{ totalCost(r) | number:'1.2-2' }}</td>
                  <td><span class="badge" [class]="r.isDisabled ? 'bg-secondary' : 'bg-success'">
                    {{ (r.isDisabled ? 'Disabled' : 'Active') | abpLocalization }}
                  </span></td>
                  <td class="text-end">
                    <button class="btn btn-sm btn-outline-danger" (click)="delete(r)"><i class="fa fa-trash"></i></button>
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
export class RoutingListComponent implements OnInit {
  private service = inject(ManufacturingService);
  private confirmation = inject(ConfirmationService);
  routings: RoutingDto[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit(): void { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.service.getRoutingList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, sorting: '' })
      .subscribe({
        next: (r) => { this.routings = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; },
        error: () => { this.isLoading = false; },
      });
  }

  totalCost(r: RoutingDto): number {
    return (r.operations ?? []).reduce((s, o) => s + (o.operatingCost || 0), 0);
  }

  delete(r: RoutingDto) {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm && r.id) {
        this.service.deleteRouting(r.id).subscribe(() => this.loadData());
      }
    });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
