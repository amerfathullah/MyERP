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

@Component({
  selector: 'app-quotation-list',
  standalone: true,
  imports: [CommonModule, PageModule, MatCardModule, MatTableModule, MatButtonModule, MatIconModule, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './quotation-list.component.html',
  styleUrls: ['./quotation-list.component.scss'],
})
export class QuotationListComponent implements OnInit {
  private router = inject(Router);
  quotations: any[] = [];
  isLoading = false;
  displayedColumns = ['quotationNumber', 'date', 'customerName', 'grandTotal', 'status', 'actions'];

  ngOnInit(): void {
    // TODO: Wire to QuotationAppService proxy
  }

  create(): void {
    this.router.navigate(['/sales/quotations/new']);
  }

  convertToSalesOrder(id: string): void {
    // TODO: Call API to convert quote → SO
  }
}
