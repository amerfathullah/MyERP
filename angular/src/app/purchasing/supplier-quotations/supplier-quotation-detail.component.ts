import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { SupplierQuotationService } from '../../proxy/purchasing/supplier-quotation.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';
import type { SupplierQuotationDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-supplier-quotation-detail',
  standalone: true,
  imports: [
    CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent,
    StatusBadgeComponent, LoadingOverlayComponent, DocumentWorkflowComponent,
    ActivityLogComponent, DocumentConnectionsComponent, CompanyCurrencyPipe],
  template: `
    <app-breadcrumb />
    <abp-page [title]="sq?.quotationNumber ?? ('::SupplierQuotation' | abpLocalization)">
      @if (isLoading()) {
        <app-loading-overlay />
      } @else if (sq) {
        <app-document-workflow
          [actions]="workflowActions"
          (actionFired)="onWorkflowAction($event)" />

        <!-- Header Cards -->
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ '::Status' | abpLocalization }}</div>
              <app-status-badge [status]="sq.status" />
              @if (isExpired) {
                <span class="badge bg-warning text-dark mt-1">{{ '::Expired' | abpLocalization }}</span>
              }
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ '::Supplier' | abpLocalization }}</div>
              <div class="fw-bold">
                <a [routerLink]="['/suppliers', sq.supplierId]">{{ sq.supplierName ?? sq.supplierId }}</a>
              </div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ '::Date' | abpLocalization }}</div>
              <div>{{ sq.transactionDate | date:'dd/MM/yyyy' }}</div>
              @if (getValidTill()) {
                <small class="text-muted">{{ '::ValidTill' | abpLocalization }}: {{ getValidTill() | date:'dd/MM/yyyy' }}</small>
              }
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ '::GrandTotal' | abpLocalization }}</div>
              <div class="fs-4 fw-bold text-primary">{{ sq.grandTotal | companyCurrency }}</div>
            </div></div>
          </div>
        </div>

        <!-- Items Table -->
        <div class="card mb-4">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ '::Items' | abpLocalization }}</h6>
            <span class="badge bg-secondary">{{ sq.items?.length ?? 0 }} {{ '::Items' | abpLocalization }}</span>
          </div>
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of sq.items; track $index) {
                    <tr>
                      <td>
                        <a [routerLink]="['/inventory/items', item.itemId]" class="fw-medium">{{ item.itemName ?? item.itemId }}</a>
                        @if (item.description) {
                          <small class="d-block text-muted">{{ item.description }}</small>
                        }
                      </td>
                      <td class="text-end">{{ item.quantity | number:'1.0-2' }} <small class="text-muted">{{ item.uom ?? 'Unit' }}</small></td>
                      <td class="text-end">{{ item.rate | companyCurrency }}</td>
                      <td class="text-end fw-bold">{{ item.amount | companyCurrency }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light">
                  <tr class="fw-bold">
                    <td colspan="3" class="text-end">{{ '::GrandTotal' | abpLocalization }}</td>
                    <td class="text-end">{{ sq.grandTotal | companyCurrency }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        </div>

        <!-- Document Connections -->
        @if (sq.id) {
          <app-document-connections documentType="SupplierQuotation" [documentId]="sq.id!" />
        }

        <!-- Activity Log -->
        @if (sq.id) {
          <app-activity-log documentType="SupplierQuotation" [documentId]="sq.id!" />
        }
      }
    </abp-page>
  `
})
export class SupplierQuotationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(SupplierQuotationService);
  private conversionService = inject(PurchaseConversionService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  sq: SupplierQuotationDto | null = null;
  isLoading = signal(true);

  get workflowActions(): WorkflowAction[] {
    if (!this.sq) return [];
    const actions: WorkflowAction[] = [];
    const status = this.sq.status as any;

    if (status === 'Draft' || status === 0) {
      actions.push({ name: 'submit', label: this.l.instant('::Submit'), icon: 'paper-plane', color: 'primary' });
    }
    if (status === 'Submitted' || status === 1) {
      actions.push({ name: 'createPO', label: this.l.instant('::CreatePurchaseOrder'), icon: 'shopping-cart', color: 'success' });
      actions.push({ name: 'cancel', label: this.l.instant('::Cancel'), icon: 'ban', color: 'danger' });
    }
    if (status === 'Cancelled' || status === 4) {
      actions.push({ name: 'amend', label: this.l.instant('::Amend'), icon: 'file-circle-plus', color: 'success' });
    }
    return actions;
  }

  get isExpired(): boolean {
    const validTill = (this.sq as any)?.validTill;
    if (!validTill) return false;
    return new Date(validTill) < new Date();
  }

  getValidTill(): string | null {
    return (this.sq as any)?.validTill ?? null;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadQuotation(id);
  }

  private loadQuotation(id: string): void {
    this.isLoading.set(true);
    this.service.get(id).subscribe({
      next: (result) => { this.sq = result; this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }

  onWorkflowAction(action: string): void {
    const id = this.sq!.id!;
    switch (action) {
      case 'submit':
        this.service.submit(id).subscribe({
          next: () => { this.toaster.success('::SuccessfullySubmitted'); this.loadQuotation(id); },
          error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
        });
        break;
      case 'createPO':
        this.conversionService.convertSupplierQuotationToPurchaseOrder(id).subscribe({
          next: (po) => {
            this.toaster.success('::PurchaseOrderCreated');
            this.router.navigate(['/purchasing/orders', po.id]);
          },
          error: (err: any) => this.toaster.error(err?.error?.error?.data?.reason || err?.error?.error?.message || '::ConversionFailed'),
        });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
          if (status === Confirmation.Status.confirm) {
            this.service.cancel(id).subscribe({
              next: () => { this.toaster.success('::SuccessfullyCancelled'); this.loadQuotation(id); },
              error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
            });
          }
        });
        break;
      case 'amend':
        this.router.navigate(['/purchasing/supplier-quotations/new'], {
          queryParams: { amendFrom: id }
        });
        break;
    }
  }
}
