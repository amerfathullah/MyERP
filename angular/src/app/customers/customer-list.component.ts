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
  selector: 'app-customer-list',
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
  templateUrl: './customer-list.component.html',
  styleUrls: ['./customer-list.component.scss'],
})
export class CustomerListComponent implements OnInit {
  customers: any[] = [];
  isLoading = false;
  displayedColumns = ['name', 'customerCode', 'tin', 'phone', 'email', 'status'];

  constructor(public readonly list: ListService) {}

  ngOnInit(): void {
    // TODO: Wire up to CustomerAppService proxy
  }

  createCustomer(): void {
    // TODO: Open create dialog
  }
}
