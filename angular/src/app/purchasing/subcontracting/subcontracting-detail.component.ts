import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';

@Component({
  selector: 'app-subcontracting-detail',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, DocumentWorkflowComponent, BreadcrumbComponent, ActivityLogComponent, StatusBadgeComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="order()?.orderNumber ?? 'Subcontracting Order'">
      @if (!order()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="row mb-3">
          <div class="col-md-8">
            <div class="card">
              <div class="card-header d-flex justify-content-between">
                <h5 class="mb-0">{{ order()!.orderNumber }}</h5>
                <app-status-badge [status]="order()!.status + ''" />
              </div>
              <div class="card-body">
                <div class="row g-3">
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'Supplier' | abpLocalization }}</small><strong>{{ order()!.supplierName ?? (order()!.supplierId | slice:0:8) }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'Date' | abpLocalization }}</small><strong>{{ order()!.orderDate | date:'dd/MM/yyyy' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'GrandTotal' | abpLocalization }}</small><strong class="fs-5">{{ order()!.grandTotal | number:'1.2-2' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'Currency' | abpLocalization }}</small><strong>{{ order()!.currencyCode ?? 'MYR' }}</strong></div>
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <app-document-workflow [actions]="getActions()" (actionClicked)="onAction($event)" />
          </div>
        </div>

        @if ((order()!.items ?? []).length > 0) {
          <div class="card mb-3">
            <div class="card-header"><h6 class="mb-0">{{ 'Items' | abpLocalization }} (FG)</h6></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ReceivedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                </tr></thead>
                <tbody>
                  @for (item of order()!.items ?? []; track item.id) {
                    <tr>
                      <td>{{ item.description ?? item.itemId }}</td>
                      <td class="text-end">{{ item.quantity }}</td>
                      <td class="text-end">
                        <span [class.text-success]="(item.receivedQty ?? 0) >= item.quantity" [class.text-warning]="(item.receivedQty ?? 0) > 0 && (item.receivedQty ?? 0) < item.quantity">
                          {{ item.receivedQty ?? 0 }}
                        </span>
                      </td>
                      <td class="text-end">{{ item.unitPrice | number:'1.2-2' }}</td>
                      <td class="text-end fw-semibold">{{ item.quantity * item.unitPrice | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        @if ((order()!.suppliedItems ?? []).length > 0) {
          <div class="card mb-3">
            <div class="card-header"><h6 class="mb-0">{{ 'SuppliedMaterials' | abpLocalization }} (RM)</h6></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Required' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Transferred' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Consumed' | abpLocalization }}</th>
                </tr></thead>
                <tbody>
                  @for (rm of order()!.suppliedItems ?? []; track rm.id) {
                    <tr>
                      <td>{{ rm.description ?? rm.itemId }}</td>
                      <td class="text-end">{{ rm.requiredQty }}</td>
                      <td class="text-end">{{ rm.transferredQty ?? 0 }}</td>
                      <td class="text-end">{{ rm.consumedQty ?? 0 }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <app-activity-log documentType="SubcontractingOrder" [documentId]="order()!.id!" />
      }
    </abp-page>
  `,
})
export class SubcontractingDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(SubcontractingService);
  private confirmation = inject(ConfirmationService);
  order = signal<any>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.service.getOrder(id).subscribe(o => this.order.set(o));
  }

  getActions(): WorkflowAction[] {
    const s = this.order()?.status;
    const actions: WorkflowAction[] = [];
    if (s === 0) actions.push({ name: 'submit', label: 'Submit', icon: 'fa-paper-plane', color: 'btn-outline-primary' });
    if (s === 1 || s === 2) actions.push({ name: 'close', label: 'Close', icon: 'fa-lock', color: 'btn-outline-dark' });
    if (s !== 0 && s !== 5) actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa-ban', color: 'btn-outline-danger' });
    return actions;
  }

  onAction(name: string): void {
    const id = this.order()!.id!;
    const reload = () => this.service.getOrder(id).subscribe(o => this.order.set(o));
    switch (name) {
      case 'submit': this.service.submitOrder(id).subscribe(reload); break;
      case 'close': this.service.cancelOrder(id).subscribe(reload); break;
      case 'cancel': this.confirmation.warn('CancelConfirmationMessage', 'Confirm').subscribe(s => {
        if (s === Confirmation.Status.confirm) this.service.cancelOrder(id).subscribe(reload);
      }); break;
    }
  }
}
