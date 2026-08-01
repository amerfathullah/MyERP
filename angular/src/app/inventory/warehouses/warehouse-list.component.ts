import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { BranchService } from '../../proxy/core/branch.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import type { WarehouseDto } from '../../proxy/inventory/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-warehouse-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, PaginationComponent],
  templateUrl: './warehouse-list.component.html',
  styleUrls: ['./warehouse-list.component.scss'],
})
export class WarehouseListComponent implements OnInit {
  private warehouseService = inject(WarehouseService);
  private router = inject(Router);
  private branchService = inject(BranchService);

  warehouses: WarehouseDto[] = [];
  totalCount = 0;
  isLoading = false;
  pageSize = 10;
  currentPage = 0;
  branchNames = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.loadWarehouses(this.currentPage * this.pageSize, this.pageSize);
    this.branchService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: (res) => {
        const map: Record<string, string> = {};
        (res.items ?? []).forEach((b: any) => { map[b.id] = b.name ?? b.branchCode ?? '—'; });
        this.branchNames.set(map);
      },
      error: () => {}
    });
  }

  loadWarehouses(skipCount: number, maxResultCount: number): void {
    this.isLoading = true;
    this.warehouseService.getList({ skipCount, maxResultCount, sorting: '' }).subscribe((res) => {
      this.warehouses = res.items ?? [];
      this.totalCount = res.totalCount ?? 0;
      this.isLoading = false;
    });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.loadWarehouses(event.pageIndex * this.pageSize, this.pageSize);
  }

  createWarehouse(): void {
    this.router.navigate(['/inventory/warehouses/new']);
  }
}
