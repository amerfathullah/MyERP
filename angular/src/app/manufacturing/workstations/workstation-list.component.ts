import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { WorkstationService, type WorkstationDto } from '../../proxy/manufacturing/manufacturing-config.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-workstation-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'Workstations' | abpLocalization">
      <div class="d-flex justify-content-between gap-2 mb-3">
        <input type="text" class="form-control form-control-sm" style="width:200px"
          [(ngModel)]="searchTerm" (keyup.enter)="loadData()" placeholder="Search...">
        <button class="btn btn-primary btn-sm" routerLink="/manufacturing/workstations/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewWorkstation' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <div class="text-center py-3"><i class="fa fa-spinner fa-spin fa-2x"></i></div> }

      @if (!isLoading && workstations.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-gear fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoWorkstationsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'Type' | abpLocalization }}</th>
              <th>{{ 'Capacity' | abpLocalization }}</th>
              <th class="text-end">{{ 'HourRate' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (ws of workstations; track ws.id) {
                <tr>
                  <td><a [routerLink]="['/manufacturing/workstations', ws.id]">{{ ws.name }}</a></td>
                  <td>{{ ws.workstationType ?? '—' }}</td>
                  <td>{{ ws.productionCapacity }}</td>
                  <td class="text-end">{{ ws.hourRate | number:'1.2-2' }}</td>
                  <td><span class="badge" [class]="ws.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ ws.isActive ? 'Active' : 'Inactive' }}
                  </span></td>
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
export class WorkstationListComponent implements OnInit {
  private service = inject(WorkstationService);
  workstations: WorkstationDto[] = [];
  isLoading = false;
  searchTerm = '';
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit(): void { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize } as any).subscribe({
      next: (r) => { this.workstations = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
