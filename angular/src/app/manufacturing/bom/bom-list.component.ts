import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

interface BomDto {
  id: string;
  itemId: string;
  itemName?: string;
  bomNumber?: string;
  quantity: number;
  totalCost: number;
  isActive: boolean;
  isDefault: boolean;
}

@Component({
  selector: 'app-bom-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'BillOfMaterials' | abpLocalization">
      <div class="d-flex justify-content-between mb-3">
        <input type="text" class="form-control form-control-sm" style="width:200px"
          [(ngModel)]="searchTerm" (keyup.enter)="loadData()" placeholder="Search...">
        <a routerLink="/manufacturing/bom/new" class="btn btn-primary">
          <i class="fa fa-plus me-1"></i>{{ 'NewBOM' | abpLocalization }}
        </a>
      </div>

      @if (isLoading()) {
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      } @else if (boms().length === 0) {
        <div class="text-center py-5 text-muted">
          <i class="fa fa-sitemap fa-3x mb-3"></i>
          <p>{{ 'NoBOMsYet' | abpLocalization }}</p>
          <a routerLink="/manufacturing/bom/new" class="btn btn-primary"><i class="fa fa-plus me-1"></i>{{ 'NewBOM' | abpLocalization }}</a>
        </div>
      } @else {
        <div class="card">
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th class="ps-3">{{ 'BOMNumber' | abpLocalization }}</th>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                  <th class="text-end">{{ 'TotalCost' | abpLocalization }}</th>
                  <th class="text-center">{{ 'Status' | abpLocalization }}</th>
                  <th class="pe-3"></th>
                </tr>
              </thead>
              <tbody>
                @for (bom of boms(); track bom.id) {
                  <tr>
                    <td class="ps-3 fw-bold">{{ bom.bomNumber ?? '—' }}</td>
                    <td>{{ bom.itemName ?? bom.itemId }}</td>
                    <td class="text-end font-monospace">{{ bom.quantity | number:'1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ bom.totalCost | number:'1.2-2' }}</td>
                    <td class="text-center">
                      @if (bom.isDefault) { <span class="badge bg-primary">Default</span> }
                      @else if (bom.isActive) { <span class="badge bg-success">Active</span> }
                      @else { <span class="badge bg-secondary">Inactive</span> }
                    </td>
                    <td class="pe-3 text-end">
                      <a [routerLink]="['/manufacturing/bom', bom.id]" class="btn btn-sm btn-outline-primary"><i class="fa fa-eye"></i></a>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
      <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
  </abp-page>
  `,
})
export class BomListComponent implements OnInit {
  private http = inject(HttpClient);
  boms = signal<BomDto[]>([]);
  isLoading = signal(true);
  searchTerm = '';
  totalCount = 0;
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData() {
    this.isLoading.set(true);
    const params: any = { skipCount: String(this.currentPage * this.pageSize), maxResultCount: String(this.pageSize) };
    if (this.searchTerm) params.filter = this.searchTerm;
    this.http.get<any>('/api/app/manufacturing/bom-list', { params })
      .subscribe({
        next: res => { this.boms.set(res.items ?? res ?? []); this.totalCount = res.totalCount ?? 0; this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
