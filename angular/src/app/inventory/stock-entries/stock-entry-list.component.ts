import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatMenuModule } from '@angular/material/menu';
import { RouterModule } from '@angular/router';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { StockEntryStore } from '../store/stock-entry.store';

@Component({
  selector: 'app-stock-entry-list',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, MatCardModule, MatTableModule,
    MatButtonModule, MatIconModule, MatPaginatorModule, MatMenuModule,
    RouterModule, StatusBadgeComponent, LoadingOverlayComponent,
  ],
  templateUrl: './stock-entry-list.component.html',
  styleUrls: ['./stock-entry-list.component.scss'],
})
export class StockEntryListComponent implements OnInit {
  readonly store = inject(StockEntryStore);
  displayedColumns = ['entryNumber', 'entryType', 'postingDate', 'status', 'actions'];

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: '' });
  }

  onPageChange(event: PageEvent): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: '',
    });
  }
}
