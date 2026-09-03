import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { MaterialRequestStore } from '../store/material-request.store';
import { MaterialRequestService } from '../../proxy/purchasing/material-request.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import type { MaterialRequestDto } from '../../proxy/purchasing/dtos/models';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-material-request-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent, ActivityLogComponent, DocumentWorkflowComponent, DocumentConnectionsComponent],
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
  private supplierService = inject(SupplierService);
  private toaster = inject(ToasterService);

  entity: MaterialRequestDto | null = null;
  actionLoading = signal(false);
  suppliers = signal<any[]>([]);
  showConvertPanel = signal(false);
  selectedSupplierId = '';

  // Per-item supplier split (Create > Split by Supplier)
  showSplitPanel = signal(false);
  splitSelections: Record<string, { supplierId: string; quantity: number }> = {};

  get workflowActions(): WorkflowAction[] {
    if (!this.entity) return [];
    const s = this.entity.status;
    const actions: WorkflowAction[] = [];
    if (s === 0) { // Draft
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (s === 1) { // Submitted
      // Purchase or Subcontracting with remaining un-ordered qty (ERPNext PR #47012 / commit 5a524854de)
      if ((this.entity.requestType === 0 || (this.entity.requestType as number) === 5) && (this.entity.perOrdered ?? 0) < 100) {
        actions.push({ name: 'convertToPO', label: 'Create Purchase Order', icon: 'file-invoice', color: 'success' });
        actions.push({ name: 'splitBySupplier', label: 'Split by Supplier', icon: 'code-branch', color: 'secondary' });
      }
      if (this.entity.requestType === 1 || this.entity.requestType === 2) { // Transfer/Issue
        actions.push({ name: 'createSE', label: 'Create Stock Entry', icon: 'truck', color: 'info' });
      }
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  get isOverdue(): boolean {
    if (!this.entity?.requiredByDate) return false;
    if (this.entity.status !== 1) return false; // only for submitted
    return new Date(this.entity.requiredByDate) < new Date(new Date().toDateString());
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({
      next: (result) => { this.entity = result; },
      error: () => {}
    });
  }

  loadSuppliers(): void {
    if (this.suppliers().length) return;
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: res => this.suppliers.set(res.items ?? []),
      error: () => {}
    });
  }

  getTypeLabel(type: number | undefined): string {
    return ['Purchase', 'Material Transfer', 'Material Issue', 'Manufacture'][type ?? 0] ?? 'Purchase';
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled', 'Rejected'][status ?? 0] ?? 'Draft';
  }

  onWorkflowAction(action: string): void {
    switch (action) {
      case 'submit': this.submit(); break;
      case 'convertToPO': this.convertToPO(); break;
      case 'splitBySupplier': this.openSplitPanel(); break;
      case 'createSE': this.createStockEntry(); break;
      case 'cancel': this.cancelMR(); break;
    }
  }

  private submit(): void {
    this.actionLoading.set(true);
    this.service.submit(this.entity!.id!).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySubmitted');
        this.actionLoading.set(false);
        this.reload();
      },
      error: (err: any) => { this.actionLoading.set(false); this.toaster.error(err?.error?.error?.message || '::OperationFailed'); }
    });
  }

  private cancelMR(): void {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.cancel(this.entity!.id!).subscribe({
          next: () => {
            this.toaster.success('::SuccessfullyCancelled');
            this.reload();
          },
          error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
        });
      }
    });
  }

  convertToPO(): void {
    this.loadSuppliers();
    this.showConvertPanel.set(true);
  }

  confirmConvertToPO(): void {
    if (!this.selectedSupplierId) {
      this.toaster.warn('::PleaseSelectSupplier');
      return;
    }
    this.actionLoading.set(true);
    this.purchaseConversionService.convertMaterialRequestToPurchaseOrder(this.entity!.id!, this.selectedSupplierId).subscribe({
      next: (po) => {
        this.actionLoading.set(false);
        this.showConvertPanel.set(false);
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/purchasing/orders', po.id]);
      },
      error: () => { this.actionLoading.set(false); this.toaster.error('::ConversionFailed'); },
    });
  }

  openSplitPanel(): void {
    this.loadSuppliers();
    this.splitSelections = {};
    for (const item of this.entity?.items ?? []) {
      const pending = (item.quantity ?? 0) - (item.orderedQuantity ?? 0);
      if (pending > 0.0001) {
        this.splitSelections[item.id!] = { supplierId: '', quantity: pending };
      }
    }
    this.showSplitPanel.set(true);
  }

  pendingQty(item: any): number {
    return Math.max(0, (item.quantity ?? 0) - (item.orderedQuantity ?? 0));
  }

  confirmSplitConvert(): void {
    const items = Object.entries(this.splitSelections)
      .filter(([, sel]) => sel.supplierId && sel.quantity > 0)
      .map(([materialRequestItemId, sel]) => ({
        materialRequestItemId,
        supplierId: sel.supplierId,
        quantity: sel.quantity,
      }));

    if (!items.length) {
      this.toaster.warn('::PleaseSelectSupplier');
      return;
    }

    this.actionLoading.set(true);
    this.purchaseConversionService.createPurchaseOrdersFromMaterialRequest({
      materialRequestId: this.entity!.id!,
      items,
    }).subscribe({
      next: (result) => {
        this.actionLoading.set(false);
        this.showSplitPanel.set(false);
        this.toaster.success('::SuccessfullyCreated');
        if (result.purchaseOrders?.length === 1) {
          this.router.navigate(['/purchasing/orders', result.purchaseOrders[0].purchaseOrderId]);
        } else {
          this.reload();
        }
      },
      error: (err: any) => { this.actionLoading.set(false); this.toaster.error(err?.error?.error?.message || '::ConversionFailed'); },
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

  private reload(): void {
    this.service.get(this.entity!.id!).subscribe({
      next: (result) => { this.entity = result; },
      error: () => {}
    });
  }
}
