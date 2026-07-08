import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatMenuModule } from '@angular/material/menu';
import { JournalEntryStore } from '../store/journal-entry.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-journal-entry-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    PageModule,
    LocalizationModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatMenuModule,
    StatusBadgeComponent,
    LoadingOverlayComponent,
  ],
  templateUrl: './journal-entry-list.component.html',
  styleUrls: ['./journal-entry-list.component.scss'],
})
export class JournalEntryListComponent implements OnInit {
  readonly store = inject(JournalEntryStore);

  displayedColumns = ['entryNumber', 'postingDate', 'referenceType', 'totalDebit', 'status', 'actions'];

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: 'postingDate DESC' });
  }

  onPageChange(event: PageEvent): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: 'postingDate DESC',
    });
  }
}
