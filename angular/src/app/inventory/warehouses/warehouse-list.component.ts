import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import type { WarehouseDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-warehouse-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationModule, MatTableModule, MatPaginatorModule, StatusBadgeComponent],
  templateUrl: './warehouse-list.component.html',
  styleUrls: ['./warehouse-list.component.scss'],
})
export class WarehouseListComponent implements OnInit {
  private warehouseService = inject(WarehouseService);
  private router = inject(Router);

  warehouses: WarehouseDto[] = [];
  totalCount = 0;
  isLoading = false;
  displayedColumns = ['warehouseCode', 'name', 'city', 'state', 'isActive'];

  ngOnInit(): void {
    this.loadWarehouses(0, 20);
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
    this.loadWarehouses(event.pageIndex * event.pageSize, event.pageSize);
  }

  createWarehouse(): void {
    this.router.navigate(['/inventory/warehouses/new']);
  }
}
