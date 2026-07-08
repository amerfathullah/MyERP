import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { QuotationStore } from '../store/quotation.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-quotation-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationModule, MatCardModule, MatTableModule, MatPaginatorModule, MatButtonModule, MatIconModule, MatMenuModule, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './quotation-list.component.html',
  styleUrls: ['./quotation-list.component.scss'],
})
export class QuotationListComponent implements OnInit {
  readonly store = inject(QuotationStore);
  displayedColumns = ['quotationNumber', 'issueDate', 'grandTotal', 'status', 'actions'];

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: 'issueDate DESC' });
  }

  onPageChange(event: PageEvent): void {
    this.store.load({
      skipCount: event.pageIndex * event.pageSize,
      maxResultCount: event.pageSize,
      sorting: 'issueDate DESC',
    });
  }
}
