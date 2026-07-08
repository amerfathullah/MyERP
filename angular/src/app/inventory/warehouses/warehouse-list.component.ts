import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import type { WarehouseDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-warehouse-list',
  standalone: true,
  imports: [CommonModule, PageModule, MatCardModule, MatTableModule, MatButtonModule, MatIconModule, MatPaginatorModule, StatusBadgeComponent, LoadingOverlayComponent],
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
