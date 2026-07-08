import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListService } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent } from '../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-supplier-list',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    StatusBadgeComponent,
    LoadingOverlayComponent,
  ],
  providers: [ListService],
  templateUrl: './supplier-list.component.html',
  styleUrls: ['./supplier-list.component.scss'],
})
export class SupplierListComponent implements OnInit {
  suppliers: any[] = [];
  isLoading = false;
  displayedColumns = ['name', 'supplierCode', 'tin', 'phone', 'email', 'status'];

  constructor(public readonly list: ListService) {}

  ngOnInit(): void {
    // TODO: Wire up to SupplierAppService proxy
  }

  createSupplier(): void {
    // TODO: Open create dialog
  }
}
