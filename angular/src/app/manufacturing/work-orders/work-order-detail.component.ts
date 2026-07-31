import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import type { WorkOrderDto, BomOperationDto, WorkOrderJobCardDto } from '../../proxy/manufacturing/models';
import { HttpClient } from '@angular/common/http';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';

@Component({
  selector: 'app-work-order-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, ActivityLogComponent, DocumentConnectionsComponent, VoucherLedgerComponent],
  template: `
    <abp-page [title]="wo()?.workOrderNumber ?? ('Manufacturing:WorkOrders' | abpLocalization)">
  <app-breadcrumb />
      @if (isLoading()) { <app-loading-overlay /> }
      @if (wo(); as w) {
        <div class="row mb-4">
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">{{ 'Status' | abpLocalization }}</small>
                <div class="mt-1"><app-status-badge [status]="getStatus(w.status)" /></div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">{{ '::Quantity' | abpLocalization }}</small>
                <div class="fw-bold mt-1">{{ w.quantity | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">{{ 'Manufacturing:Progress' | abpLocalization }}</small>
                <div class="fw-bold mt-1">{{ w.percentComplete | number:'1.0-0' }}%</div>
                <div class="progress mt-1" style="height: 6px;">
                  <div class="progress-bar bg-success" [style.width.%]="w.percentComplete"></div>
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">{{ '::Produced' | abpLocalization }}</small>
                <div class="fw-bold mt-1">{{ w.producedQuantity | number:'1.2-2' }} / {{ w.quantity | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Material Readiness Banner (auto-populated) -->
        @if (materialAvailability().length > 0 && !hasShortage()) {
          <div class="alert alert-success py-2 d-flex align-items-center mb-3">
            <i class="fa fa-check-circle me-2"></i>
            <span>{{ '::AllMaterialsReady' | abpLocalization }}</span>
          </div>
        }
        @if (materialAvailability().length > 0 && hasShortage()) {
          <div class="alert alert-warning py-2 d-flex align-items-center mb-3">
            <i class="fa fa-triangle-exclamation me-2"></i>
            <span>{{ '::MaterialShortageWarning' | abpLocalization }}</span>
          </div>
        }

        <!-- Quality Inspection Advisory (for FG items requiring QI) -->
        @if (qiRequired() && (wo()!.status ?? 0) >= 3) {
          @if (qiStatus() === 'Accepted') {
            <div class="alert alert-success py-2 d-flex align-items-center mb-3">
              <i class="fa fa-microscope me-2"></i>
              <span>{{ '::QualityInspectionPassed' | abpLocalization }}</span>
            </div>
          } @else if (qiStatus() === 'Rejected') {
            <div class="alert alert-danger py-2 d-flex align-items-center mb-3">
              <i class="fa fa-microscope me-2"></i>
              <span>{{ '::QualityInspectionFailed' | abpLocalization }}</span>
            </div>
          } @else {
            <div class="alert alert-warning py-2 d-flex align-items-center mb-3">
              <i class="fa fa-microscope me-2"></i>
              <span>{{ '::QualityInspectionPending' | abpLocalization }}</span>
            </div>
          }
        }

        <div class="d-flex gap-2 mb-3">
          @if (wo()!.status === 1) {
            <button class="btn btn-primary btn-sm" (click)="start()"><i class="fa fa-play me-1"></i>{{ '::StartProduction' | abpLocalization }}</button>
          }
          @if (wo()!.status === 3) {
            <button class="btn btn-success btn-sm" (click)="recordProduction()"><i class="fa fa-check me-1"></i>{{ '::RecordProduction' | abpLocalization }}</button>
            <button class="btn btn-info btn-sm" (click)="recordConsumption()"><i class="fa fa-flask me-1"></i>{{ '::RecordConsumption' | abpLocalization }}</button>
            <button class="btn btn-outline-primary btn-sm" (click)="openManufactureDialog()"><i class="fa fa-industry me-1"></i>{{ '::Manufacture' | abpLocalization }}</button>
            <button class="btn btn-warning btn-sm" (click)="stop()"><i class="fa fa-pause me-1"></i>{{ '::Stop' | abpLocalization }}</button>
          }
          @if (wo()!.status === 5) {
            <button class="btn btn-primary btn-sm" (click)="unstop()"><i class="fa fa-play me-1"></i>{{ '::Resume' | abpLocalization }}</button>
          }
          @if (wo()!.status === 4 && (wo()!.producedQuantity ?? 0) > (wo()!.disassembledQuantity ?? 0)) {
            <button class="btn btn-outline-warning btn-sm" (click)="disassemble()" [disabled]="isDisassembling()">
              @if (isDisassembling()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              <i class="fa fa-rotate-left me-1"></i>{{ '::Disassemble' | abpLocalization }}
            </button>
          }
          @if (wo()!.status! >= 1 && wo()!.status! < 4) {
            <button class="btn btn-outline-secondary btn-sm" (click)="createStockEntry()"><i class="fa fa-truck me-1"></i>{{ '::MaterialTransfer' | abpLocalization }}</button>
          }
          @if (wo()!.status! >= 1 && wo()!.status! <= 3 && jobCards().length === 0) {
            <button class="btn btn-outline-primary btn-sm" (click)="createJobCards()" [disabled]="isCreatingJobCards()">
              @if (isCreatingJobCards()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              <i class="fa fa-id-card me-1"></i>{{ '::CreateJobCards' | abpLocalization }}
            </button>
          }
          @if (wo()!.status! >= 1 && wo()!.status! <= 3) {
            <button class="btn btn-outline-danger btn-sm" (click)="cancel()"><i class="fa fa-times me-1"></i>{{ '::Cancel' | abpLocalization }}</button>
          }
          @if (wo()!.status! >= 1 && wo()!.status! <= 3) {
            <button class="btn btn-outline-info btn-sm" (click)="checkMaterialAvailability()" [disabled]="isCheckingMaterials()">
              @if (isCheckingMaterials()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              <i class="fa fa-boxes-stacked me-1"></i> {{ '::CheckMaterialAvailability' | abpLocalization }}
            </button>
          }
        </div>

        <!-- Material Availability Results -->
        @if (materialAvailability().length > 0) {
          <div class="card mb-3" [class.border-danger]="hasShortage()" [class.border-success]="!hasShortage()">
            <div class="card-header d-flex justify-content-between align-items-center py-2"
                 [class.bg-danger]="hasShortage()" [class.text-white]="hasShortage()"
                 [class.bg-success]="!hasShortage()" [class.bg-opacity-10]="true">
              <span>
                <i class="fa" [class.fa-triangle-exclamation]="hasShortage()" [class.fa-check-circle]="!hasShortage()"></i>
                {{ hasShortage() ? ('::MaterialShortageDetected' | abpLocalization) : ('::AllMaterialsAvailable' | abpLocalization) }}
              </span>
              <button class="btn btn-sm btn-outline-secondary" (click)="materialAvailability.set([])">
                <i class="fa fa-times"></i>
              </button>
            </div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr class="table-light">
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Required' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Transferred' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Pending' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Available' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Shortage' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (mat of materialAvailability(); track mat.itemId) {
                    <tr [class.table-danger]="!mat.hasSufficientStock" [class.table-success]="mat.hasSufficientStock">
                      <td>
                        <span class="fw-medium">{{ mat.itemName }}</span>
                        <br><small class="text-muted">{{ mat.itemCode }}</small>
                      </td>
                      <td class="text-end font-monospace">{{ mat.requiredQty | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ mat.transferredQty | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ mat.pendingQty | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ mat.availableQty | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace" [class.text-danger]="mat.shortage > 0" [class.fw-bold]="mat.shortage > 0">
                        {{ mat.shortage > 0 ? (mat.shortage | number:'1.2-2') : '—' }}
                      </td>
                      <td class="text-center">
                        @if (mat.hasSufficientStock) {
                          <span class="badge bg-success"><i class="fa fa-check"></i></span>
                        } @else {
                          <span class="badge bg-danger"><i class="fa fa-times"></i></span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-4 mb-2">
                <small class="text-muted d-block">{{ 'Manufacturing:Item' | abpLocalization }}</small>
                <span>{{ w.itemName ?? w.itemId }}</span>
              </div>
              @if (w.plannedStartDate) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ 'StartDate' | abpLocalization }}</small>
                  <span>{{ w.plannedStartDate | date:'dd/MM/yyyy' }}</span>
                </div>
              }
              @if (w.plannedEndDate) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ 'EndDate' | abpLocalization }}</small>
                  <span>{{ w.plannedEndDate | date:'dd/MM/yyyy' }}</span>
                </div>
              }
            </div>
          </div>
        </div>

        @if (operations().length > 0) {
          <div class="card mb-3">
            <div class="card-header fw-bold">{{ 'Manufacturing:Operations' | abpLocalization }}</div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>{{ 'Manufacturing:OperationName' | abpLocalization }}</th>
                    <th>{{ 'Manufacturing:Workstation' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Manufacturing:PlannedTime' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (op of operations(); track op.id) {
                    <tr>
                      <td>{{ op.sequenceId }}</td>
                      <td>{{ op.description ?? op.operationId }}</td>
                      <td>{{ op.workstationId ?? '—' }}</td>
                      <td class="text-end">{{ op.timeInMins }} min</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        @if (jobCards().length > 0) {
          <div class="card mb-3">
            <div class="card-header fw-bold d-flex justify-content-between align-items-center">
              <span><i class="fa fa-id-card me-2"></i>{{ 'JobCards' | abpLocalization }}</span>
              <span class="badge bg-primary">{{ jobCards().length }}</span>
            </div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>{{ 'Operation' | abpLocalization }}</th>
                    <th class="text-end">{{ 'CompletedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ 'TimeSpent' | abpLocalization }}</th>
                    <th style="width: 120px;">{{ 'Manufacturing:Progress' | abpLocalization }}</th>
                    <th>{{ 'Status' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (jc of jobCards(); track jc.id) {
                    <tr>
                      <td>{{ jc.sequenceId }}</td>
                      <td><a [routerLink]="['/manufacturing/job-cards', jc.id]" class="text-decoration-none">{{ jc.operationName ?? 'Operation ' + jc.sequenceId }}</a></td>
                      <td class="text-end">{{ jc.completedQty ?? 0 | number:'1.2-2' }} / {{ jc.forQuantity ?? 0 | number:'1.2-2' }}</td>
                      <td class="text-end">{{ jc.totalTimeInMins ?? 0 | number:'1.0-0' }} min</td>
                      <td>
                        <div class="progress" style="height: 6px;">
                          <div class="progress-bar"
                            [class.bg-success]="getJcCompletionPct(jc) >= 100"
                            [class.bg-primary]="getJcCompletionPct(jc) > 0 && getJcCompletionPct(jc) < 100"
                            [class.bg-secondary]="getJcCompletionPct(jc) === 0"
                            [style.width.%]="getJcCompletionPct(jc)">
                          </div>
                        </div>
                      </td>
                      <td><span class="badge" [ngClass]="getJcStatusClass(jc.status)">{{ getJcStatusLabel(jc.status) }}</span></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        @if (w.requiredItems && w.requiredItems.length > 0) {
          <div class="card">
            <div class="card-header fw-bold">{{ '::RequiredMaterials' | abpLocalization }}</div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>{{ 'Manufacturing:Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Required' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Transferred' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Consumed' | abpLocalization }}</th>
                    <th style="width: 140px;">{{ 'Manufacturing:TransferProgress' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of w.requiredItems; track item.id) {
                    <tr>
                      <td>{{ item.itemName }}</td>
                      <td class="text-end">{{ item.requiredQuantity | number:'1.2-2' }}</td>
                      <td class="text-end">{{ item.transferredQuantity | number:'1.2-2' }}</td>
                      <td class="text-end">{{ item.consumedQuantity | number:'1.2-2' }}</td>
                      <td>
                        <div class="progress" style="height: 8px;">
                          <div class="progress-bar"
                            [class.bg-success]="getTransferPct(item) >= 100"
                            [class.bg-primary]="getTransferPct(item) > 0 && getTransferPct(item) < 100"
                            [class.bg-secondary]="getTransferPct(item) === 0"
                            [style.width.%]="getTransferPct(item)">
                          </div>
                        </div>
                        <small class="text-muted">{{ getTransferPct(item) | number:'1.0-0' }}%</small>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <app-document-connections [documentType]="'WorkOrder'" [documentId]="w.id!" />

        <app-activity-log documentType="WorkOrder" [documentId]="w.id!" />

        @if ((w.status ?? 0) >= 3) {
          <app-voucher-ledger
            voucherType="WorkOrder"
            [voucherId]="w.id!" />
        }
      }

      <!-- Manufacture Qty Dialog -->
      @if (showManufactureDialog()) {
        <div class="modal d-block" tabindex="-1" style="background: rgba(0,0,0,0.5);">
          <div class="modal-dialog modal-sm modal-dialog-centered">
            <div class="modal-content">
              <div class="modal-header py-2">
                <h6 class="modal-title"><i class="fa fa-industry me-2"></i>{{ '::Manufacture' | abpLocalization }}</h6>
                <button type="button" class="btn-close" (click)="showManufactureDialog.set(false)"></button>
              </div>
              <div class="modal-body">
                <label class="form-label">{{ '::CompletedQtyThisRun' | abpLocalization }}</label>
                <input type="number" class="form-control" [min]="0.001" [max]="(wo()?.quantity ?? 0) - (wo()?.producedQuantity ?? 0)"
                       [ngModel]="manufactureQty()" (ngModelChange)="manufactureQty.set($event)" step="0.01">
                <small class="text-muted">{{ '::Remaining' | abpLocalization }}: {{ (wo()?.quantity ?? 0) - (wo()?.producedQuantity ?? 0) | number:'1.2-2' }}</small>
              </div>
              <div class="modal-footer py-2">
                <button class="btn btn-sm btn-secondary" (click)="showManufactureDialog.set(false)">{{ '::Cancel' | abpLocalization }}</button>
                <button class="btn btn-sm btn-primary" (click)="createManufactureEntry()" [disabled]="!manufactureQty() || manufactureQty() <= 0">
                  <i class="fa fa-check me-1"></i>{{ '::Manufacture' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class WorkOrderDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(ManufacturingService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private manufacturingService = inject(ManufacturingService);
  private http = inject(HttpClient);
  private l = inject(LocalizationService);

  wo = signal<WorkOrderDto | null>(null);
  operations = signal<BomOperationDto[]>([]);
  jobCards = signal<WorkOrderJobCardDto[]>([]);
  isLoading = signal(false);
  materialAvailability = signal<any[]>([]);
  isCheckingMaterials = signal(false);
  isDisassembling = signal(false);
  isCreatingJobCards = signal(false);
  qiRequired = signal(false);
  qiStatus = signal<string>('');

  hasShortage(): boolean {
    return this.materialAvailability().some(m => !m.hasSufficientStock);
  }

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading.set(true);
      this.service.getWorkOrder(id).subscribe({
        next: w => {
          this.wo.set(w);
          this.isLoading.set(false);
          if (w.bomId) {
            this.service.getBom(w.bomId).subscribe({
              next: bom => this.operations.set(bom.operations ?? []),
            });
          }
          // Load job cards for this work order
          this.service.getWorkOrderJobCards(w.id!).subscribe({
            next: res => this.jobCards.set(res.items ?? []),
            error: () => {},
          });
          // Auto-check material readiness for production-ready WOs
          const status = w.status ?? 0;
          if (status >= 1 && status <= 3) {
            this.checkMaterialAvailability(true);
          }
          // Check QI requirement for FG item (non-blocking advisory)
          if (w.itemId && status >= 3) {
            this.loadQiStatus(w.id!, w.itemId);
          }
        },
        error: () => this.isLoading.set(false),
      });
    }
  }

  start() {
    const id = this.wo()!.id!;
    this.service.startWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('::SuccessfullyStarted'); },
      error: () => this.toaster.error('::OperationFailed'),
    });
  }

  recordProduction() {
    const id = this.wo()!.id!;
    const qty = prompt(this.l.instant('::EnterProducedQuantity'));
    if (!qty || isNaN(+qty) || +qty <= 0) return;
    this.service.recordProduction(id, +qty).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('::SuccessfullyRecorded'); },
      error: () => this.toaster.error(this.l.instant('::OperationFailed')),
    });
  }

  stop() {
    const id = this.wo()!.id!;
    this.service.stopWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('::SuccessfullyStopped'); },
      error: () => this.toaster.error('::OperationFailed'),
    });
  }

  unstop() {
    const id = this.wo()!.id!;
    this.manufacturingService.unstopWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('::SuccessfullyUpdated'); },
      error: () => this.toaster.error('::OperationFailed'),
    });
  }

  disassemble() {
    const wo = this.wo()!;
    const available = (wo.producedQuantity ?? 0) - (wo.disassembledQuantity ?? 0);
    const qty = prompt(this.l.instant('::EnterDisassemblyQty') + ` (max: ${available})`, String(available));
    if (!qty) return;
    const parsedQty = parseFloat(qty);
    if (isNaN(parsedQty) || parsedQty <= 0 || parsedQty > available) {
      this.toaster.warn(this.l.instant('::InvalidQuantity'));
      return;
    }
    this.isDisassembling.set(true);
    this.http.post<any>('/api/app/manufacturing/work-order/disassembly', {
      workOrderId: wo.id, quantity: parsedQty
    }).subscribe({
      next: (result) => {
        this.isDisassembling.set(false);
        this.toaster.success(this.l.instant('::DisassemblyCreated') + ` — ${result.itemCount} items`);
        // Reload WO data to show updated disassembled qty
        this.service.getWorkOrder(wo.id!).subscribe(w => this.wo.set(w));
      },
      error: (err: any) => {
        this.isDisassembling.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      }
    });
  }

  cancel() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      const id = this.wo()!.id!;
      this.service.cancelWorkOrder(id).subscribe({
        next: w => { this.wo.set(w); this.toaster.success('::SuccessfullyCancelled'); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    });
  }

  recordConsumption() {
    const wo = this.wo()!;
    const items = wo.requiredItems ?? [];
    if (!items.length) {
      this.toaster.warn('::NoRawMaterialsDefined');
      return;
    }
    this.confirmation.warn('::RecordConsumptionConfirmation', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;

    // Build consumption items from WO BOM items (use transferred qty as max)
    const consumptionItems = items
      .filter((i: any) => (i.transferredQuantity ?? 0) > 0)
      .map((i: any) => ({
        itemId: i.itemId,
        quantity: i.transferredQuantity ?? i.requiredQuantity,
      }));

    if (!consumptionItems.length) {
      this.toaster.warn('::NoMaterialsTransferredYet');
      return;
    }

    this.isLoading.set(true);
    this.manufacturingService.createMaterialConsumption({
      workOrderId: wo.id,
      items: consumptionItems,
    }).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        this.toaster.success('::SuccessfullyRecorded');
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
    }); // end confirmation subscribe
  }

  createStockEntry() {
    const woId = this.wo()!.id!;
    this.confirmation.warn('::CreateMaterialTransferConfirmation', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.isLoading.set(true);
      this.manufacturingService.createMaterialTransferForManufacture(woId).subscribe({
      next: (se) => {
        this.isLoading.set(false);
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/inventory/stock-entries', se.stockEntryId]);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
    }); // end confirmation subscribe
  }

  showManufactureDialog = signal(false);
  manufactureQty = signal(0);

  openManufactureDialog() {
    const wo = this.wo()!;
    const remaining = (wo.quantity ?? 0) - (wo.producedQuantity ?? 0);
    this.manufactureQty.set(remaining);
    this.showManufactureDialog.set(true);
  }

  createManufactureEntry() {
    const wo = this.wo()!;
    const fgQty = this.manufactureQty();
    if (!fgQty || fgQty <= 0) return;
    this.showManufactureDialog.set(false);

    this.isLoading.set(true);
    this.manufacturingService.createManufactureStockEntry({
      workOrderId: wo.id,
      fgQuantity: fgQty,
      processLossQty: 0,
    }).subscribe({
      next: (se) => {
        this.isLoading.set(false);
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/inventory/stock-entries', se.stockEntryId]);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      },
    });
  }

  getStatus(s: number | undefined): string {
    const keys = ['::Draft', '::Submitted', '::NotStarted', '::InProcess', '::Completed', '::Stopped', '::Cancelled'];
    return this.l.instant(keys[s ?? 0] ?? '::Draft');
  }

  getTransferPct(item: { requiredQuantity?: number; transferredQuantity?: number }): number {
    const req = item.requiredQuantity ?? 0;
    if (req <= 0) return 0;
    return Math.min(100, ((item.transferredQuantity ?? 0) / req) * 100);
  }

  getJcStatusLabel(status: number | undefined): string {
    const keys = ['::Open', '::InProgress', '::MaterialTransferred', '::Completed', '::OnHold', '::Cancelled'];
    return this.l.instant(keys[status ?? 0] ?? '::Open');
  }

  getJcStatusClass(status: number | undefined): string {
    return ['bg-secondary', 'bg-primary', 'bg-info', 'bg-success', 'bg-warning text-dark', 'bg-danger'][status ?? 0] ?? 'bg-secondary';
  }

  getJcCompletionPct(jc: WorkOrderJobCardDto): number {
    const total = jc.forQuantity ?? 0;
    if (total <= 0) return 0;
    return Math.min(100, ((jc.completedQty ?? 0) / total) * 100);
  }

  checkMaterialAvailability(silent = false): void {
    const w = this.wo();
    if (!w?.id) return;
    this.isCheckingMaterials.set(true);
    this.http.get<any[]>(`/api/app/manufacturing/work-order/${w.id}/material-availability`).subscribe({
      next: (result) => {
        this.materialAvailability.set(result ?? []);
        this.isCheckingMaterials.set(false);
        if (!silent) {
          if (this.hasShortage()) {
            this.toaster.warn(this.l.instant('::MaterialShortageDetected'));
          } else {
            this.toaster.success(this.l.instant('::AllMaterialsAvailable'));
          }
        }
      },
      error: () => {
        this.isCheckingMaterials.set(false);
      },
    });
  }

  createJobCards(): void {
    const w = this.wo();
    if (!w?.id) return;
    this.isCreatingJobCards.set(true);
    this.http.post<any[]>(`/api/app/manufacturing/work-order/${w.id}/create-job-cards`, {}).subscribe({
      next: (result) => {
        this.isCreatingJobCards.set(false);
        this.jobCards.set(result ?? []);
        this.toaster.success(this.l.instant('::SuccessfullyCreated'));
      },
      error: (err) => {
        this.isCreatingJobCards.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      },
    });
  }

  private loadQiStatus(workOrderId: string, itemId: string): void {
    this.http.get<any>(`/api/app/quality-inspection`, {
      params: { referenceType: 'WorkOrder', referenceId: workOrderId, itemId }
    }).subscribe({
      next: (res) => {
        const inspections = res?.items ?? [];
        if (inspections.length === 0) {
          this.http.get<any>(`/api/app/item/${itemId}`).subscribe({
            next: (item) => {
              this.qiRequired.set(item?.inspectionRequiredBeforeDelivery ?? false);
            },
            error: () => {},
          });
        } else {
          this.qiRequired.set(true);
          const latest = inspections[0];
          this.qiStatus.set(latest.status === 1 ? 'Accepted' : latest.status === 2 ? 'Rejected' : 'Pending');
        }
      },
      error: () => {},
    });
  }
}
