import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-bom-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <div class="container-fluid">
      <app-breadcrumb />
      @if (bom(); as b) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="mb-0">
              <i class="fas fa-sitemap me-2"></i>{{ b.itemName || b.itemId }}
              @if (b.isActive) {
                <span class="badge bg-success ms-2">{{ 'Active' | abpLocalization }}</span>
              } @else {
                <span class="badge bg-secondary ms-2">{{ 'Inactive' | abpLocalization }}</span>
              }
              @if (b.isDefault) {
                <span class="badge bg-primary ms-2">{{ 'Default' | abpLocalization }}</span>
              }
            </h5>
            <div class="btn-group btn-group-sm">
              <a [routerLink]="['/manufacturing/bom', b.id]" class="btn btn-outline-primary">
                <i class="fas fa-edit me-1"></i>{{ 'Edit' | abpLocalization }}
              </a>
            </div>
          </div>
          <div class="card-body">
            <div class="row mb-4">
              <div class="col-md-3">
                <small class="text-muted">{{ 'Quantity' | abpLocalization }}</small>
                <div class="fw-bold fs-5">{{ b.quantity | number:'1.2-2' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'MaterialCost' | abpLocalization }}</small>
                <div class="fw-bold fs-5 text-primary">{{ b.totalCost | number:'1.2-2' }}</div>
              </div>
              @if (b.operatingCost > 0) {
                <div class="col-md-3">
                  <small class="text-muted">{{ 'OperatingCost' | abpLocalization }}</small>
                  <div class="fw-bold fs-5 text-info">{{ b.operatingCost | number:'1.2-2' }}</div>
                </div>
                <div class="col-md-3">
                  <small class="text-muted">{{ 'TotalCost' | abpLocalization }}</small>
                  <div class="fw-bold fs-5 text-success">{{ (b.totalCost + b.operatingCost) | number:'1.2-2' }}</div>
                </div>
              }
            </div>

            <!-- Materials -->
            <h6 class="mb-3"><i class="fas fa-cube me-2"></i>{{ 'Materials' | abpLocalization }} ({{ b.items?.length || 0 }})</h6>
            @if (b.items?.length > 0) {
              <table class="table table-hover table-sm mb-4">
                <thead class="table-light">
                  <tr>
                    <th style="width: 40px">#</th>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th class="text-end" style="width: 100px">{{ 'Quantity' | abpLocalization }}</th>
                    <th class="text-end" style="width: 120px">{{ 'Rate' | abpLocalization }}</th>
                    <th class="text-end" style="width: 120px">{{ 'Amount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of b.items; track item.itemId; let i = $index) {
                    <tr>
                      <td class="text-muted">{{ i + 1 }}</td>
                      <td>
                        {{ item.description || item.itemId }}
                        @if (item.isPhantom) {
                          <span class="badge bg-warning text-dark ms-1">Phantom</span>
                        }
                        @if (item.subBomId) {
                          <span class="badge bg-info ms-1">Sub-Assembly</span>
                        }
                      </td>
                      <td class="text-end">{{ item.quantity | number:'1.2-4' }}</td>
                      <td class="text-end">{{ item.rate | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold">{{ item.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="table-light">
                    <td colspan="4" class="text-end fw-bold">{{ 'Total' | abpLocalization }}</td>
                    <td class="text-end fw-bold">{{ b.totalCost | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            }

            <!-- Operations -->
            @if (b.operations?.length > 0) {
              <h6 class="mb-3"><i class="fas fa-cogs me-2"></i>{{ 'Operations' | abpLocalization }} ({{ b.operations.length }})</h6>
              <table class="table table-hover table-sm mb-4">
                <thead class="table-light">
                  <tr>
                    <th style="width: 70px">{{ 'Sequence' | abpLocalization }}</th>
                    <th>{{ 'Operation' | abpLocalization }}</th>
                    <th>{{ 'Workstation' | abpLocalization }}</th>
                    <th class="text-end" style="width: 100px">{{ 'Time' | abpLocalization }} (min)</th>
                    <th class="text-end" style="width: 100px">{{ 'BatchSize' | abpLocalization }}</th>
                    <th class="text-end" style="width: 120px">{{ 'Cost' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (op of b.operations; track op.sequenceId) {
                    <tr>
                      <td><span class="badge bg-secondary">{{ op.sequenceId }}</span></td>
                      <td>{{ op.description || op.operationId }}</td>
                      <td>{{ op.workstationId || '—' }}</td>
                      <td class="text-end">{{ op.timeInMins | number:'1.0-0' }}</td>
                      <td class="text-end">{{ op.batchSize || '—' }}</td>
                      <td class="text-end fw-bold">{{ op.operatingCost | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="table-light">
                    <td colspan="5" class="text-end fw-bold">{{ 'TotalOperatingCost' | abpLocalization }}</td>
                    <td class="text-end fw-bold">{{ b.operatingCost | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>
            }

            <!-- Secondary Items -->
            @if (b.secondaryItems?.length > 0) {
              <h6 class="mb-3"><i class="fas fa-recycle me-2"></i>{{ 'SecondaryItems' | abpLocalization }}</h6>
              <table class="table table-hover table-sm">
                <thead class="table-light">
                  <tr>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th>{{ 'Type' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                    <th class="text-end">{{ 'CostAllocation' | abpLocalization }} %</th>
                  </tr>
                </thead>
                <tbody>
                  @for (si of b.secondaryItems; track si.itemId) {
                    <tr>
                      <td>{{ si.description || si.itemId }}</td>
                      <td>
                        @switch (si.secondaryItemType) {
                          @case (0) { <span class="badge bg-info">Co-Product</span> }
                          @case (1) { <span class="badge bg-success">By-Product</span> }
                          @case (2) { <span class="badge bg-warning text-dark">Scrap</span> }
                        }
                      </td>
                      <td class="text-end">{{ si.quantity | number:'1.2-2' }}</td>
                      <td class="text-end">{{ si.costAllocationPercentage | number:'1.1-1' }}%</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        </div>

        <app-activity-log documentType="BillOfMaterials" [documentId]="bomId" />
      } @else {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status"></div>
        </div>
      }
    </div>
  `
})
export class BomDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private manufacturingService = inject(ManufacturingService);

  bom = signal<any>(null);
  bomId = '';

  ngOnInit(): void {
    this.bomId = this.route.snapshot.paramMap.get('id') ?? '';
    this.manufacturingService.getBom(this.bomId).subscribe({
      next: (data) => this.bom.set(data),
      error: () => this.router.navigate(['/manufacturing/bom'])
    });
  }
}
