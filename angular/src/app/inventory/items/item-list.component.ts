import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { ItemStore } from '../store/item.store';

@Component({
  selector: 'app-item-list',
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
  templateUrl: './item-list.component.html',
  styleUrls: ['./item-list.component.scss'],
})
export class ItemListComponent implements OnInit {
  readonly store = inject(ItemStore);
  private router = inject(Router);
  displayedColumns = ['itemCode', 'itemName', 'itemGroup', 'uom', 'stockQty', 'rate', 'status'];

  ngOnInit(): void {
    // TODO: Wire up to ItemAppService proxy
  }

  createItem(): void {
    // TODO: Navigate to create form or open dialog
  }
}
