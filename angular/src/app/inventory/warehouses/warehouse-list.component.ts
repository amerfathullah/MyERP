import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-warehouse-list',
  standalone: true,
  imports: [CommonModule, PageModule, MatCardModule, MatTableModule, MatButtonModule, MatIconModule, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './warehouse-list.component.html',
  styleUrls: ['./warehouse-list.component.scss'],
})
export class WarehouseListComponent implements OnInit {
  warehouses: any[] = [];
  isLoading = false;
  displayedColumns = ['warehouseCode', 'warehouseName', 'branch', 'status'];

  ngOnInit(): void {
    // TODO: Wire up to WarehouseAppService proxy
  }

  createWarehouse(): void {
    // TODO: Open dialog
  }
}
