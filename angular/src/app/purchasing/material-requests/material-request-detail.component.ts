import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { MaterialRequestStore } from '../store/material-request.store';
import { MaterialRequestService } from '../../proxy/purchasing/material-request.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import type { MaterialRequestDto } from '../../proxy/purchasing/dtos/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-material-request-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, ActivityLogComponent],
  templateUrl: './material-request-detail.component.html',
  styleUrls: ['./material-request-detail.component.scss'],
})
export class MaterialRequestDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private purchaseConversionService = inject(PurchaseConversionService);
  readonly store = inject(MaterialRequestStore);
  private service = inject(MaterialRequestService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  entity: MaterialRequestDto | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.entity = result;
    });
  }

  getTypeLabel(type: number | undefined): string {
    return ['Purchase', 'Material Transfer', 'Material Issue', 'Manufacture'][type ?? 0] ?? 'Purchase';
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled', 'Rejected'][status ?? 0] ?? 'Draft';
  }

  submit(): void {
    this.store.submitRequest(this.entity!.id!);
    this.reloadAfterAction();
  }

  cancel(): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.cancelRequest(this.entity!.id!);
        this.reloadAfterAction();
      }
    });
  }

  convertToPO(): void {
    const supplierId = prompt('Enter Supplier ID for the Purchase Order:');
    if (!supplierId) return;
    this.purchaseConversionService.convertMaterialRequestToPurchaseOrder(this.entity!.id!, supplierId).subscribe({
      next: (po) => {
        this.toaster.success('Purchase Order created: ' + po.orderNumber);
        this.router.navigate(['/purchasing/orders', po.id]);
      },
      error: () => this.toaster.error('Conversion failed'),
    });
  }

  createStockEntry(): void {
    const purpose = this.entity!.requestType === 1 ? 'MaterialTransfer' : 'MaterialIssue';
    this.router.navigate(['/inventory/stock-entries/new'], {
      queryParams: {
        purpose,
        sourceWarehouse: (this.entity as any).sourceWarehouseId ?? '',
        targetWarehouse: (this.entity as any).targetWarehouseId ?? '',
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/purchasing/material-requests']);
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.entity!.id!).subscribe((result) => {
        this.entity = result;
      });
    }, 500);
  }
}
