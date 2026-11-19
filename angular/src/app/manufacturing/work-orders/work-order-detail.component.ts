import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import type { WorkOrderDto, BomOperationDto, WorkOrderJobCardDto, ProductionCostBreakdownDto, AlternativeFinishedGoodsDetailsDto } from '../../proxy/manufacturing/models';
import { ItemService } from '../../proxy/inventory/item.service';
import { QualityInspectionService } from '../../proxy/inventory/quality-inspection.service';
import { InspectionStatus } from '../../proxy/inventory/inspection-status.enum';
import { StockReservationService } from '../../proxy/inventory/stock-reservation.service';
import type { StockReservationEntryDto } from '../../proxy/inventory/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';

@Component({
  selector: 'app-work-order-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, ActivityLogComponent, DocumentConnectionsComponent, VoucherLedgerComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="wo()?.workOrderNumber ?? ('::Manufacturing:WorkOrders' | abpLocalization)">
  <app-breadcrumb />
      @if (isLoading()) { <app-loading-overlay /> }
      @if (wo(); as w) {
        <div class="row mb-4">
          <div class="col-md-3">
            <div class="card">
              <div class="card-body text-center">
                <small class="text-muted">{{ '::Status' | abpLocalization }}</small>
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
                <small class="text-muted">{{ '::Manufacturing:Progress' | abpLocalization }}</small>
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

        <div class="d-flex flex-wrap gap-2 mb-3">
          @if (wo()!.status === 0) {
            <a class="btn btn-outline-primary btn-sm" [routerLink]="['/manufacturing/work-orders', wo()!.id, 'edit']"><i class="fa fa-edit me-1"></i>{{ '::Edit' | abpLocalization }}</a>
            <button class="btn btn-primary btn-sm" (click)="submit()"><i class="fa fa-check me-1"></i>{{ '::Submit' | abpLocalization }}</button>
            <button class="btn btn-outline-danger btn-sm" (click)="deleteWorkOrder()"><i class="fa fa-trash me-1"></i>{{ '::Delete' | abpLocalization }}</button>
          }
          @if (wo()!.status === 1 || wo()!.status === 2) {
            <button class="btn btn-primary btn-sm" (click)="start()"><i class="fa fa-play me-1"></i>{{ '::StartProduction' | abpLocalization }}</button>
          }
          @if (wo()!.status === 3) {
            @if (hasRemainingProductionQty) {
              <button class="btn btn-success btn-sm" (click)="openProductionDialog()"><i class="fa fa-check me-1"></i>{{ '::RecordProduction' | abpLocalization }}</button>
            }
            <button class="btn btn-info btn-sm" (click)="recordConsumption()"><i class="fa fa-flask me-1"></i>{{ '::RecordConsumption' | abpLocalization }}</button>
            @if (hasRemainingProductionQty) {
              <button class="btn btn-outline-primary btn-sm" (click)="openManufactureDialog()"><i class="fa fa-industry me-1"></i>{{ '::Manufacture' | abpLocalization }}</button>
            }
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
          @if (wo()!.status === 4 || (wo()!.status! >= 1 && (wo()!.producedQuantity ?? 0) > 0)) {
            <button class="btn btn-outline-success btn-sm" (click)="createBomFromWorkOrder()" [disabled]="isCreatingBom()">
              @if (isCreatingBom()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              <i class="fa fa-sitemap me-1"></i>{{ '::CreateBom' | abpLocalization }}
            </button>
          }
          @if (hasReturnableMaterials) {
            <button class="btn btn-outline-warning btn-sm" (click)="returnExcessMaterials()" [disabled]="isReturningMaterials()">
              @if (isReturningMaterials()) { <span class="spinner-border spinner-border-sm me-1"></span> }
              <i class="fa fa-undo me-1"></i>{{ '::ReturnExcessMaterials' | abpLocalization }}
            </button>
          }
          @if ((fgConversionDetails()?.alternativeItems?.length ?? 0) > 0 && (fgConversionDetails()?.availableQty ?? 0) > 0) {
            <button class="btn btn-outline-primary btn-sm" (click)="openFgConversionModal()">
              <i class="fa fa-exchange-alt me-1"></i>{{ '::ConvertFinishedGoods' | abpLocalization }}
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
          @if (hasHeldStockReservation) {
            <button class="btn btn-outline-warning btn-sm" (click)="showUnreserveModal.set(true)">
              <i class="fa fa-ban me-1"></i>{{ '::UnreserveStock' | abpLocalization }}
            </button>
          }
        </div>

        <!-- Production Recording Dialog -->
        @if (showProductionDialog()) {
          <div class="card mb-3 border-success">
            <div class="card-header bg-success bg-opacity-10 py-2 d-flex justify-content-between align-items-center">
              <span class="fw-semibold"><i class="fa fa-industry me-1"></i> {{ '::RecordProduction' | abpLocalization }}</span>
              <button class="btn btn-sm btn-outline-secondary" (click)="cancelProductionDialog()"><i class="fa fa-times"></i></button>
            </div>
            <div class="card-body">
              <div class="row g-3">
                <div class="col-md-4">
                  <div class="text-muted small">{{ '::Remaining' | abpLocalization }}</div>
                  <div class="fw-bold fs-5">{{ pendingProductionQty | number:'1.2-2' }}</div>
                </div>
                <div class="col-md-4">
                  <label class="form-label small">{{ '::CompletedQtyThisRun' | abpLocalization }}</label>
                  <input type="number" class="form-control" [min]="0.001" [max]="allowedProductionQty"
                         [value]="productionQty()" (input)="productionQty.set(+$any($event).target.value)">
                </div>
                <div class="col-md-4">
                  <label class="form-label small">{{ '::ProcessLoss' | abpLocalization }}</label>
                  <input type="number" class="form-control" [min]="0" step="0.01"
                         [value]="processLossQty()" (input)="processLossQty.set(+$any($event).target.value)">
                  <div class="form-text">{{ '::DefectiveScrapQty' | abpLocalization }}</div>
                </div>
              </div>
              <div class="mt-3 d-flex align-items-center justify-content-between">
                <span class="badge bg-info">{{ '::TotalFG' | abpLocalization }}: {{ totalFgQty | number:'1.2-2' }} ({{ '::Good' | abpLocalization }}: {{ productionQty() | number:'1.2-2' }} + {{ '::Loss' | abpLocalization }}: {{ processLossQty() | number:'1.2-2' }})</span>
                <div class="d-flex gap-2">
                  <button class="btn btn-outline-secondary btn-sm" (click)="cancelProductionDialog()">{{ '::Cancel' | abpLocalization }}</button>
                  <button class="btn btn-success btn-sm" (click)="recordProduction()" [disabled]="isRecordingProduction() || productionQty() <= 0">
                    @if (isRecordingProduction()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                    <i class="fa fa-check me-1"></i> {{ '::Confirm' | abpLocalization }}
                  </button>
                </div>
              </div>
            </div>
          </div>
        }

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
                <small class="text-muted d-block">{{ '::Manufacturing:Item' | abpLocalization }}</small>
                <span>{{ w.itemName ?? w.itemId }}</span>
              </div>
              @if (w.plannedStartDate) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ '::StartDate' | abpLocalization }}</small>
                  <span>{{ w.plannedStartDate | date:'dd/MM/yyyy' }}</span>
                </div>
              }
              @if (w.plannedEndDate) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ '::EndDate' | abpLocalization }}</small>
                  <span>{{ w.plannedEndDate | date:'dd/MM/yyyy' }}</span>
                </div>
              }
              @if (w.actualStartDate) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ '::ActualStartDate' | abpLocalization }}</small>
                  <span>{{ w.actualStartDate | date:'dd/MM/yyyy HH:mm' }}</span>
                </div>
              }
              @if (w.actualEndDate) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ '::ActualEndDate' | abpLocalization }}</small>
                  <span>{{ w.actualEndDate | date:'dd/MM/yyyy HH:mm' }}</span>
                </div>
              }
              @if (w.leadTime !== null && w.leadTime !== undefined) {
                <div class="col-md-4 mb-2">
                  <small class="text-muted d-block">{{ '::LeadTime' | abpLocalization }}</small>
                  <span>{{ w.leadTime | number:'1.0-2' }} min</span>
                </div>
              }
            </div>
          </div>
        </div>

        @if (operations().length > 0) {
          <div class="card mb-3">
            <div class="card-header fw-bold">{{ '::Manufacturing:Operations' | abpLocalization }}</div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>{{ '::Manufacturing:OperationName' | abpLocalization }}</th>
                    <th>{{ '::Manufacturing:Workstation' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Manufacturing:PlannedTime' | abpLocalization }}</th>
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

        @if (costBreakdown(); as cb) {
          <div class="card mb-3">
            <div class="card-header fw-bold d-flex justify-content-between align-items-center">
              <span><i class="fa fa-calculator me-2"></i>{{ '::ProductionCostBreakdown' | abpLocalization }}</span>
              <span class="badge" [class.bg-danger]="(cb.costVariancePercent ?? 0) > 0" [class.bg-success]="(cb.costVariancePercent ?? 0) <= 0">
                {{ '::CostVariance' | abpLocalization }}: {{ cb.costVariancePercent ?? 0 | number:'1.1-1' }}%
              </span>
            </div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <tbody>
                  <tr><td>{{ '::TotalRmCost' | abpLocalization }}</td><td class="text-end font-monospace">{{ cb.totalRmCost | number:'1.2-2' }}</td></tr>
                  <tr><td>{{ '::ProcessLossValue' | abpLocalization }}</td><td class="text-end font-monospace">{{ cb.processLossValue | number:'1.2-2' }}</td></tr>
                  <tr><td>{{ '::AdditionalCosts' | abpLocalization }}</td><td class="text-end font-monospace">{{ cb.additionalCosts | number:'1.2-2' }}</td></tr>
                  <tr class="table-light fw-bold"><td>{{ '::TotalProductionCost' | abpLocalization }}</td><td class="text-end font-monospace">{{ cb.totalProductionCost | number:'1.2-2' }}</td></tr>
                  <tr><td>{{ '::FgUnitCost' | abpLocalization }}</td><td class="text-end font-monospace">{{ cb.fgUnitCost | number:'1.2-4' }}</td></tr>
                  <tr><td>{{ '::BomStandardCost' | abpLocalization }}</td><td class="text-end font-monospace">{{ cb.bomStandardCost | number:'1.2-4' }}</td></tr>
                  <tr [class.text-danger]="(cb.costVariance ?? 0) > 0" [class.text-success]="(cb.costVariance ?? 0) <= 0">
                    <td>{{ '::CostVariance' | abpLocalization }}</td><td class="text-end font-monospace">{{ cb.costVariance | number:'1.2-4' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        }

        @if (jobCards().length > 0) {
          <div class="card mb-3">
            <div class="card-header fw-bold d-flex justify-content-between align-items-center">
              <span><i class="fa fa-id-card me-2"></i>{{ '::JobCards' | abpLocalization }}</span>
              <span class="badge bg-primary">{{ jobCards().length }}</span>
            </div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>{{ '::Operation' | abpLocalization }}</th>
                    <th class="text-end">{{ '::CompletedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::TimeSpent' | abpLocalization }}</th>
                    <th style="width: 120px;">{{ '::Manufacturing:Progress' | abpLocalization }}</th>
                    <th>{{ '::Status' | abpLocalization }}</th>
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
                    <th>{{ '::Manufacturing:Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Required' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Transferred' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Consumed' | abpLocalization }}</th>
                    <th style="width: 140px;">{{ '::Manufacturing:TransferProgress' | abpLocalization }}</th>
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
                <div class="mb-2">
                  <label class="form-label">{{ '::CompletedQtyThisRun' | abpLocalization }}</label>
                  <input type="number" class="form-control" [min]="0.001" [max]="allowedProductionQty"
                         [ngModel]="manufactureQty()" (ngModelChange)="manufactureQty.set($event)" step="0.01">
                  <small class="text-muted">{{ '::Remaining' | abpLocalization }}: {{ allowedProductionQty | number:'1.2-2' }}</small>
                </div>
                <div class="mb-2">
                  <label class="form-label">{{ '::ProcessLossQty' | abpLocalization }}</label>
                  <input type="number" class="form-control" [min]="0" [max]="manufactureQty()"
                         [ngModel]="manufactureProcessLossQty()" (ngModelChange)="manufactureProcessLossQty.set($event ?? 0)" step="0.01">
                  <small class="text-muted">{{ '::ProcessLossHelp' | abpLocalization }}</small>
                </div>
                @if (manufactureProcessLossQty() > 0) {
                  <div class="alert alert-info py-1 px-2 mb-0 small">
                    {{ '::FinishedGoodQty' | abpLocalization }}: <strong>{{ netManufactureOutputQty | number:'1.2-2' }}</strong>
                  </div>
                }
              </div>
              <div class="modal-footer py-2">
                <button class="btn btn-sm btn-secondary" (click)="showManufactureDialog.set(false)">{{ '::Cancel' | abpLocalization }}</button>
                <button class="btn btn-sm btn-primary" (click)="createManufactureEntry()"
                        [disabled]="!manufactureQty() || manufactureQty() <= 0 || manufactureProcessLossQty() < 0 || manufactureQty() <= manufactureProcessLossQty()">
                  <i class="fa fa-check me-1"></i>{{ '::Manufacture' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Unreserve Stock Modal (PR #59424 / commit dbada3f461) -->
      @if (showUnreserveModal()) {
        <div class="modal d-block" tabindex="-1" style="background: rgba(0,0,0,0.5);">
          <div class="modal-dialog modal-md modal-dialog-centered">
            <div class="modal-content">
              <div class="modal-header py-2">
                <h6 class="modal-title"><i class="fa fa-ban me-2 text-warning"></i>{{ '::UnreserveStock' | abpLocalization }}</h6>
                <button type="button" class="btn-close" (click)="showUnreserveModal.set(false)"></button>
              </div>
              <div class="modal-body p-0">
                <table class="table table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::Item' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Reserved' | abpLocalization }}</th>
                      <th class="text-end">{{ '::HeldQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Action' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (sre of stockReservations(); track sre.id) {
                      @if (getHeldQty(sre) > 0) {
                        <tr>
                          <td>{{ sre.itemId }}</td>
                          <td class="text-end">{{ sre.reservedQty | number:'1.2-2' }}</td>
                          <td class="text-end fw-bold">{{ getHeldQty(sre) | number:'1.2-2' }}</td>
                          <td class="text-end">
                            <button class="btn btn-sm btn-outline-danger" (click)="unreserve(sre)" [disabled]="isUnreserving()">
                              @if (isUnreserving()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                              {{ '::UnreserveStock' | abpLocalization }}
                            </button>
                          </td>
                        </tr>
                      }
                    }
                  </tbody>
                </table>
              </div>
              <div class="modal-footer py-2">
                <button class="btn btn-sm btn-secondary" (click)="showUnreserveModal.set(false)">{{ '::Close' | abpLocalization }}</button>
              </div>
            </div>
          </div>
        </div>
      }

      @if (showFgConversionModal()) {
        <div class="modal d-block" tabindex="-1" style="background: rgba(0,0,0,0.45)">
          <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content">
              <div class="modal-header py-2">
                <h6 class="modal-title"><i class="fa fa-exchange-alt me-1"></i>{{ '::ConvertFinishedGoods' | abpLocalization }}</h6>
                <button type="button" class="btn-close" (click)="showFgConversionModal.set(false)" [disabled]="isConvertingFg()"></button>
              </div>
              <div class="modal-body">
                <div class="mb-3">
                  <label class="form-label small fw-semibold">{{ '::AlternativeItem' | abpLocalization }} *</label>
                  <select class="form-select form-select-sm" [ngModel]="fgConversionAltItemId()" (ngModelChange)="fgConversionAltItemId.set($event)">
                    <option value="">{{ '::Select' | abpLocalization }}...</option>
                    @for (alt of fgConversionDetails()?.alternativeItems ?? []; track alt.itemId) {
                      <option [value]="alt.itemId">{{ alt.itemCode }} — {{ alt.itemName }}</option>
                    }
                  </select>
                </div>
                <div class="mb-2">
                  <label class="form-label small fw-semibold">
                    {{ '::Quantity' | abpLocalization }}
                    ({{ '::AvailableToConvert' | abpLocalization }}: {{ fgConversionDetails()?.availableQty ?? 0 | number:'1.0-4' }})
                  </label>
                  <input type="number" class="form-control form-control-sm" min="0.0001" step="1"
                    [max]="fgConversionDetails()?.availableQty ?? 0"
                    [ngModel]="fgConversionQty()"
                    (ngModelChange)="fgConversionQty.set($event)" />
                </div>
              </div>
              <div class="modal-footer py-2">
                <button type="button" class="btn btn-sm btn-secondary" (click)="showFgConversionModal.set(false)" [disabled]="isConvertingFg()">
                  {{ '::Close' | abpLocalization }}
                </button>
                <button type="button" class="btn btn-sm btn-primary" (click)="submitFgConversion()"
                  [disabled]="!fgConversionAltItemId() || fgConversionQty() <= 0 || fgConversionQty() > (fgConversionDetails()?.availableQty ?? 0) || isConvertingFg()">
                  @if (isConvertingFg()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  {{ '::ConvertFinishedGoods' | abpLocalization }}
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
  private itemService = inject(ItemService);
  private qualityInspectionService = inject(QualityInspectionService);
  private stockReservationService = inject(StockReservationService);
  private l = inject(LocalizationService);

  wo = signal<WorkOrderDto | null>(null);
  stockReservations = signal<StockReservationEntryDto[]>([]);
  showUnreserveModal = signal(false);
  isUnreserving = signal(false);
  operations = signal<BomOperationDto[]>([]);
  jobCards = signal<WorkOrderJobCardDto[]>([]);
  isLoading = signal(false);
  materialAvailability = signal<any[]>([]);
  isCheckingMaterials = signal(false);
  isDisassembling = signal(false);
  isCreatingJobCards = signal(false);
  isCreatingBom = signal(false);
  isReturningMaterials = signal(false);
  fgConversionDetails = signal<AlternativeFinishedGoodsDetailsDto | null>(null);
  showFgConversionModal = signal(false);
  fgConversionAltItemId = signal('');
  fgConversionQty = signal(0);
  isConvertingFg = signal(false);
  qiRequired = signal(false);
  qiStatus = signal<string>('');
  costBreakdown = signal<ProductionCostBreakdownDto | null>(null);

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
          // Actual-vs-standard cost breakdown — only meaningful once some FG has been produced
          if ((w.producedQuantity ?? 0) > 0) {
            this.service.getProductionCostBreakdown(w.id!).subscribe({
              next: cb => this.costBreakdown.set(cb),
              error: () => {},
            });
            this.loadFgConversionDetails(w.id!);
          }

          if (w.id) {
            this.loadStockReservations(w.id);
          }
        },
        error: () => this.isLoading.set(false),
      });
    }
  }

  loadStockReservations(workOrderId: string): void {
    this.stockReservationService.getList({
      voucherId: workOrderId,
      skipCount: 0,
      maxResultCount: 100
    } as any).subscribe({
      next: res => this.stockReservations.set((res.items ?? []).filter(s => s.status === 1)),
      error: () => this.stockReservations.set([])
    });
  }

  getHeldQty(sre: StockReservationEntryDto): number {
    return Math.max(0, (sre.reservedQty ?? 0) - (sre.deliveredQty ?? 0) - (sre.transferredQty ?? 0) - (sre.consumedQty ?? 0));
  }

  get hasHeldStockReservation(): boolean {
    return this.stockReservations().some(sre => this.getHeldQty(sre) > 0);
  }

  get hasReturnableMaterials(): boolean {
    const w = this.wo();
    if (!w || (w.status !== 4 && w.status !== 7)) return false;
    // Per ERPNext PR #67351 (commit db34b763e9): aggregate pending qty per item because rows of one item
    // split transfers by source warehouse while consumption and returns are distributed by required qty.
    const pendingQtyByItem = new Map<string, number>();
    for (const item of w.requiredItems ?? []) {
      const key = item.itemId || item.itemName || '';
      const pending = (item.transferredQuantity ?? 0) - (item.consumedQuantity ?? 0);
      pendingQtyByItem.set(key, (pendingQtyByItem.get(key) ?? 0) + pending);
    }
    for (const qty of pendingQtyByItem.values()) {
      if (Math.round(qty * 10000) / 10000 > 0) return true;
    }
    return false;
  }

  createBomFromWorkOrder(): void {
    const w = this.wo();
    if (!w?.id || this.isCreatingBom()) return;
    this.isCreatingBom.set(true);
    this.manufacturingService.createBomFromWorkOrder(w.id).subscribe({
      next: bom => {
        this.isCreatingBom.set(false);
        this.toaster.success(this.l.instant('::SuccessfullyCreated'));
        if (bom?.id) {
          this.router.navigate(['/manufacturing/bom/detail', bom.id]);
        }
      },
      error: (err: any) => {
        this.isCreatingBom.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      },
    });
  }

  returnExcessMaterials(): void {
    const w = this.wo();
    if (!w?.id || this.isReturningMaterials()) return;
    this.confirmation.warn('::ReturnExcessMaterialsConfirmation', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.isReturningMaterials.set(true);
      this.manufacturingService.createMaterialReturnForManufacture(w.id!).subscribe({
        next: se => {
          this.isReturningMaterials.set(false);
          this.toaster.success(this.l.instant('::SuccessfullyCreated'));
          this.router.navigate(['/inventory/stock-entries', se.stockEntryId]);
        },
        error: (err: any) => {
          this.isReturningMaterials.set(false);
          this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
        },
      });
    });
  }

  loadFgConversionDetails(workOrderId: string): void {
    this.manufacturingService.getFgConversionDetails(workOrderId).subscribe({
      next: details => this.fgConversionDetails.set(details),
      error: () => this.fgConversionDetails.set(null),
    });
  }

  openFgConversionModal(): void {
    const details = this.fgConversionDetails();
    const firstAlt = details?.alternativeItems?.[0]?.itemId ?? '';
    this.fgConversionAltItemId.set(firstAlt);
    this.fgConversionQty.set(details?.availableQty ?? 0);
    this.showFgConversionModal.set(true);
  }

  submitFgConversion(): void {
    const w = this.wo();
    const altId = this.fgConversionAltItemId();
    const qty = this.fgConversionQty();
    const maxQty = this.fgConversionDetails()?.availableQty ?? 0;
    if (!w?.id || !altId || qty <= 0 || qty > maxQty || this.isConvertingFg()) return;

    this.isConvertingFg.set(true);
    this.manufacturingService.createFgConversionEntry({
      workOrderId: w.id,
      alternativeItemId: altId,
      quantity: qty,
    }).subscribe({
      next: se => {
        this.isConvertingFg.set(false);
        this.showFgConversionModal.set(false);
        this.toaster.success(this.l.instant('::SuccessfullyCreated'));
        this.router.navigate(['/inventory/stock-entries', se.stockEntryId]);
      },
      error: (err: any) => {
        this.isConvertingFg.set(false);
        this.toaster.error(err?.error?.error?.data?.detail || err?.error?.error?.message || this.l.instant('::OperationFailed'));
      },
    });
  }

  unreserve(sre: StockReservationEntryDto): void {
    if (!sre.id) return;
    this.isUnreserving.set(true);
    this.stockReservationService.cancel(sre.id).subscribe({
      next: () => {
        this.toaster.success(this.l.instant('::OperationSuccessful'));
        this.isUnreserving.set(false);
        if (this.wo()?.id) {
          this.loadStockReservations(this.wo()!.id!);
        }
      },
      error: (err: any) => {
        this.toaster.error(err.message ?? this.l.instant('::Error'));
        this.isUnreserving.set(false);
      }
    });
  }

  start() {
    const id = this.wo()!.id!;
    this.service.startWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('::SuccessfullyStarted'); },
      error: () => this.toaster.error('::OperationFailed'),
    });
  }

  submit() {
    const id = this.wo()!.id!;
    this.service.submitWorkOrder(id).subscribe({
      next: w => { this.wo.set(w); this.toaster.success('::SuccessfullySubmitted'); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  deleteWorkOrder() {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      const id = this.wo()!.id!;
      this.service.deleteWorkOrder(id).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          this.router.navigate(['/manufacturing/work-orders']);
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    });
  }

  showProductionDialog = signal(false);
  productionQty = signal(0);
  processLossQty = signal(0);
  isRecordingProduction = signal(false);

  openProductionDialog() {
    this.productionQty.set(this.pendingProductionQty || this.allowedProductionQty);
    this.processLossQty.set(0);
    this.showProductionDialog.set(true);
  }

  cancelProductionDialog() {
    this.showProductionDialog.set(false);
  }

  get pendingProductionQty(): number {
    const wo = this.wo();
    if (!wo) return 0;
    const manufactured = (wo.producedQuantity ?? 0) + (wo.processLossQty ?? 0);
    return Math.max(0, Math.round(((wo.quantity ?? 0) - manufactured) * 1000) / 1000);
  }

  get allowedProductionQty(): number {
    const wo = this.wo();
    if (!wo) return 0;
    const qty = wo.quantity ?? 0;
    const manufactured = (wo.producedQuantity ?? 0) + (wo.processLossQty ?? 0);
    const overproductionPct = wo.overproductionPercentage ?? 5;
    const maxAllowed = qty + (qty * overproductionPct) / 100;
    return Math.max(0, Math.round((maxAllowed - manufactured) * 1000) / 1000);
  }

  get hasRemainingProductionQty(): boolean {
    return this.allowedProductionQty > 0;
  }

  get totalFgQty(): number {
    return this.productionQty() + this.processLossQty();
  }

  recordProduction() {
    const id = this.wo()!.id!;
    const qty = this.productionQty();
    const loss = this.processLossQty();
    if (qty <= 0) return;
    this.isRecordingProduction.set(true);
    this.service.recordProduction(id, qty, loss).subscribe({
      next: w => {
        this.wo.set(w);
        this.showProductionDialog.set(false);
        this.isRecordingProduction.set(false);
        this.toaster.success('::SuccessfullyRecorded');
      },
      error: () => {
        this.isRecordingProduction.set(false);
        this.toaster.error(this.l.instant('::OperationFailed'));
      },
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
    this.manufacturingService.createDisassemblyStockEntry({
      workOrderId: wo.id, quantity: parsedQty
    } as any).subscribe({
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
  manufactureProcessLossQty = signal(0);

  get netManufactureOutputQty(): number {
    return Math.max(0, Math.round((this.manufactureQty() - this.manufactureProcessLossQty()) * 1000) / 1000);
  }

  openManufactureDialog() {
    this.manufactureQty.set(this.pendingProductionQty || this.allowedProductionQty);
    this.manufactureProcessLossQty.set(0);
    this.showManufactureDialog.set(true);
  }

  createManufactureEntry() {
    const wo = this.wo()!;
    const fgQty = this.manufactureQty();
    const processLossQty = this.manufactureProcessLossQty();
    if (!fgQty || fgQty <= 0 || processLossQty < 0 || fgQty <= processLossQty) return;
    this.showManufactureDialog.set(false);

    this.isLoading.set(true);
    this.manufacturingService.createManufactureStockEntry({
      workOrderId: wo.id,
      fgQuantity: fgQty,
      processLossQty,
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
    this.manufacturingService.getMaterialAvailability(w.id).subscribe({
      next: (result: any) => {
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
    this.manufacturingService.createJobCardsForWorkOrder(w.id).subscribe({
      next: (result: any) => {
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
    this.itemService.get(itemId).subscribe({
      next: (item: any) => {
        const required = item?.inspectionRequiredBeforeDelivery ?? false;
        this.qiRequired.set(required);
        if (!required) {
          this.qiStatus.set('');
          return;
        }
        const woCompanyId = this.wo()?.companyId;
        this.qualityInspectionService.getList({
          companyId: woCompanyId,
          itemId,
          skipCount: 0,
          maxResultCount: 50,
        }).subscribe({
          next: res => {
            const items = res?.items ?? [];
            const linked = items.filter(q => q.referenceId === workOrderId);
            const match = (linked.length > 0 ? linked : items)[0];
            if (!match) {
              this.qiStatus.set('');
            } else if (match.status === InspectionStatus.Accepted) {
              this.qiStatus.set('Accepted');
            } else if (match.status === InspectionStatus.Rejected) {
              this.qiStatus.set('Rejected');
            } else {
              this.qiStatus.set('');
            }
          },
          error: () => {},
        });
      },
      error: () => {},
    });
  }
}
