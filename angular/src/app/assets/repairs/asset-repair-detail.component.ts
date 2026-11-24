import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { AssetRepairService } from '../../proxy/assets/asset-repair.service';
import type { AssetRepairDto } from '../../proxy/assets/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-asset-repair-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, DocumentWorkflowComponent, BreadcrumbComponent, ActivityLogComponent, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <app-breadcrumb />
    <abp-page [title]="'::AssetRepairDetails' | abpLocalization">
      @if (!repair()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="row mb-3">
          <div class="col-md-8">
            <div class="card">
              <div class="card-header d-flex justify-content-between align-items-center"><h5 class="mb-0">{{ repair()!.repairDescription ?? 'Repair' }}</h5>
                <div class="d-flex align-items-center gap-2">
                  @if (repair()!.status === 0) {
                    <a class="btn btn-sm btn-outline-secondary" [routerLink]="['/assets/repairs', repair()!.id, 'edit']">
                      <i class="fa fa-pen me-1"></i>{{ '::Edit' | abpLocalization }}
                    </a>
                  }
                  <app-status-badge [status]="getRepairStatusLabel(repair()!.status)" />
                </div>
              </div>
              <div class="card-body">
                <div class="row g-3">
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::FailureDate' | abpLocalization }}</small><strong>{{ repair()!.failureDate | date:'dd/MM/yyyy' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::CompletionDate' | abpLocalization }}</small><strong>{{ repair()!.completionDate ? (repair()!.completionDate | date:'dd/MM/yyyy') : '—' }}</strong></div>
                  <div class="col-md-4"><small class="text-muted d-block">{{ '::RepairCost' | abpLocalization }}</small><strong class="fs-5">{{ repair()!.repairCost | number:'1.2-2' }}</strong></div>
                  <div class="col-md-4"><small class="text-muted d-block">{{ '::ConsumedItemsCost' | abpLocalization }}</small><strong class="fs-5">{{ (repair()!.consumedItemsCost ?? 0) | number:'1.2-2' }}</strong></div>
                  <div class="col-md-4"><small class="text-muted d-block">{{ '::TotalRepairCost' | abpLocalization }}</small><strong class="fs-5 text-primary">{{ (repair()!.totalRepairCost ?? repair()!.repairCost ?? 0) | number:'1.2-2' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::Capitalize' | abpLocalization }}</small>
                    @if (repair()!.capitalizeRepairCost) {
                      <strong class="text-success">Yes (+{{ repair()!.increaseInAssetLife }} months)</strong>
                    } @else { <strong>No</strong> }
                  </div>
                  @if (repair()!.downtime) {
                    <div class="col-md-6"><small class="text-muted d-block">{{ '::Downtime' | abpLocalization }}</small><strong>{{ repair()!.downtime }}</strong></div>
                  }
                  @if (repair()!.repairDescription) {
                    <div class="col-12"><small class="text-muted d-block">{{ '::Description' | abpLocalization }}</small><p>{{ repair()!.repairDescription }}</p></div>
                  }
                </div>
                @if ((repair()!.stockItems?.length ?? 0) > 0) {
                  <hr />
                  <h6 class="mb-2">{{ '::ConsumedStockItems' | abpLocalization }}</h6>
                  <table class="table table-sm table-bordered mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ '::Item' | abpLocalization }}</th>
                        <th class="text-end">{{ '::Qty' | abpLocalization }}</th>
                        <th class="text-end">{{ '::ValuationRate' | abpLocalization }}</th>
                        <th class="text-end">{{ '::TotalValue' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (item of repair()!.stockItems; track item.id ?? $index) {
                        <tr>
                          <td>{{ item.itemName || item.itemId }}</td>
                          <td class="text-end">{{ (item.qty ?? 0) | number:'1.0-4' }}</td>
                          <td class="text-end">{{ (item.valuationRate ?? 0) | number:'1.2-4' }}</td>
                          <td class="text-end">{{ (item.totalValue ?? ((item.qty ?? 0) * (item.valuationRate ?? 0))) | number:'1.2-2' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                }
                @if ((repair()!.invoices?.length ?? 0) > 0) {
                  <hr />
                  <h6 class="mb-2">{{ '::PurchaseInvoices' | abpLocalization }}</h6>
                  <table class="table table-sm table-bordered mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ '::PurchaseInvoice' | abpLocalization }}</th>
                        <th class="text-end">{{ '::RepairCost' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (inv of repair()!.invoices; track inv.id) {
                        <tr>
                          <td>{{ inv.purchaseInvoiceNumber || inv.purchaseInvoiceId }}</td>
                          <td class="text-end">{{ inv.repairCost | number:'1.2-2' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                }
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <app-document-workflow [actions]="getActions()" (actionClicked)="onAction($event)" />
          </div>
        </div>
        <app-activity-log documentType="AssetRepair" [documentId]="repair()!.id!" />
      }
    </abp-page>
  `,
})
export class AssetRepairDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(AssetRepairService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  repair = signal<AssetRepairDto | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.service.get(id).subscribe({
        next: r => this.repair.set(r),
        error: (err: any) => this.handleError(err),
      });
    }
  }

  getActions(): WorkflowAction[] {
    const s = this.repair()?.status;
    const actions: WorkflowAction[] = [];
    if (s === 0) actions.push({ name: 'complete', label: '::Complete', icon: 'check', color: 'success' });
    if (s === 0 || s === 1) actions.push({ name: 'cancel', label: '::Cancel', icon: 'ban', color: 'danger' });
    return actions;
  }

  onAction(name: string): void {
    const id = this.repair()!.id!;
    const reload = () => this.service.get(id).subscribe({
      next: r => this.repair.set(r),
      error: (err: any) => this.handleError(err),
    });
    switch (name) {
      case 'complete':
        this.service.complete(id).subscribe({ next: reload, error: (err: any) => this.handleError(err) });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmationMessage', '::Confirm').subscribe(s => {
          if (s === Confirmation.Status.confirm) {
            this.service.cancel(id).subscribe({ next: reload, error: (err: any) => this.handleError(err) });
          }
        });
        break;
    }
  }

  getRepairStatusLabel(status?: number): string {
    return ['Pending', 'Completed', 'Cancelled'][status ?? 0] ?? 'Pending';
  }

  private handleError(err: any): void {
    this.toaster.error(err?.error?.error?.message || '::OperationFailed');
  }
}
