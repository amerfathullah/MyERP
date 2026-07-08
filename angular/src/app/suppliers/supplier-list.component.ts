import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatMenuModule } from '@angular/material/menu';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../shared/components/loading-overlay/loading-overlay.component';
import { SupplierService } from '../proxy/purchasing/supplier.service';
import type { SupplierDto } from '../proxy/purchasing/models';

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
    MatPaginatorModule,
    MatMenuModule,
    StatusBadgeComponent,
    LoadingOverlayComponent,
  ],
  templateUrl: './supplier-list.component.html',
  styleUrls: ['./supplier-list.component.scss'],
})
export class SupplierListComponent implements OnInit {
  private supplierService = inject(SupplierService);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);

  suppliers: SupplierDto[] = [];
  totalCount = 0;
  isLoading = false;
  displayedColumns = ['name', 'supplierCode', 'tin', 'phone', 'email', 'actions'];

  ngOnInit(): void {
    this.loadSuppliers(0, 20);
  }

  loadSuppliers(skipCount: number, maxResultCount: number): void {
    this.isLoading = true;
    this.supplierService.getList({ skipCount, maxResultCount, sorting: '' }).subscribe((res) => {
      this.suppliers = res.items ?? [];
      this.totalCount = res.totalCount ?? 0;
      this.isLoading = false;
    });
  }

  onPageChange(event: PageEvent): void {
    this.loadSuppliers(event.pageIndex * event.pageSize, event.pageSize);
  }

  createSupplier(): void {
    this.router.navigate(['/suppliers/new']);
  }

  delete(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.supplierService.delete(id).subscribe(() => {
          this.suppliers = this.suppliers.filter(s => s.id !== id);
          this.totalCount--;
        });
      }
    });
  }
}
