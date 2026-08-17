import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { MasterProductionScheduleService } from '../../proxy/manufacturing/master-production-schedule.service';
import type { MasterProductionScheduleDto } from '../../proxy/manufacturing/models';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-master-production-schedule-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? schedule?.scheduleNumber : 'NewMasterProductionSchedule') | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        @if (isEdit() && status !== 0) {
          <div class="alert" [class.alert-success]="status===1" [class.alert-secondary]="status===4">
            {{ 'Status' | abpLocalization }}: {{ statusLabel() }}
          </div>
        }

        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.postingDate" [disabled]="readOnly()" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.fromDate" [disabled]="readOnly()" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ToDate' | abpLocalization }}</label>
            <input type="text" class="form-control" [value]="schedule?.toDate ? (schedule?.toDate | date:'dd/MM/yyyy') : '—'" disabled />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ParentWarehouse' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.parentWarehouseId" [disabled]="readOnly()">
              <option value="">--</option>
              @for (w of warehouses(); track w.id) { <option [value]="w.id">{{ w.warehouseName }}</option> }
            </select>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/manufacturing/master-production-schedules">{{ 'Cancel' | abpLocalization }}</a>
          @if (!readOnly()) {
            <button class="btn btn-primary" (click)="save()" [disabled]="saving()"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          }
          @if (isEdit() && status === 0) {
            <button class="btn btn-success" (click)="submit()" [disabled]="saving()"><i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}</button>
          }
          @if (isEdit() && status === 1) {
            <button class="btn btn-outline-danger" (click)="cancelSchedule()" [disabled]="saving()"><i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
          }
        </div>
      </div></div>

      @if (isEdit()) {
        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ 'StagedSalesOrders' | abpLocalization }} ({{ schedule?.salesOrders?.length ?? 0 }})</h6>
            @if (status === 0) {
              <button class="btn btn-sm btn-outline-primary" (click)="fetchSalesOrders()" [disabled]="saving()">
                <i class="fa fa-download me-1"></i>{{ 'GetSalesOrders' | abpLocalization }}
              </button>
            }
          </div>
          @if ((schedule?.salesOrders?.length ?? 0) > 0) {
            <table class="table table-sm mb-0">
              <thead><tr><th>{{ 'Date' | abpLocalization }}</th><th class="text-end">{{ 'GrandTotal' | abpLocalization }}</th><th>{{ 'Status' | abpLocalization }}</th></tr></thead>
              <tbody>
                @for (so of schedule?.salesOrders; track so.salesOrderId) {
                  <tr>
                    <td>{{ so.salesOrderDate | date:'dd/MM/yyyy' }}</td>
                    <td class="text-end">{{ so.grandTotal | number:'1.2-2' }}</td>
                    <td>{{ so.status }}</td>
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="text-muted small mb-0">{{ 'NoStagedSalesOrdersHint' | abpLocalization }}</p>
          }
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ 'StagedMaterialRequests' | abpLocalization }} ({{ schedule?.materialRequests?.length ?? 0 }})</h6>
            @if (status === 0) {
              <button class="btn btn-sm btn-outline-primary" (click)="fetchMaterialRequests()" [disabled]="saving()">
                <i class="fa fa-download me-1"></i>{{ 'GetMaterialRequests' | abpLocalization }}
              </button>
            }
          </div>
          @if ((schedule?.materialRequests?.length ?? 0) > 0) {
            <table class="table table-sm mb-0">
              <thead><tr><th>{{ 'RequestDate' | abpLocalization }}</th></tr></thead>
              <tbody>
                @for (mr of schedule?.materialRequests; track mr.materialRequestId) {
                  <tr><td>{{ mr.materialRequestDate | date:'dd/MM/yyyy' }}</td></tr>
                }
              </tbody>
            </table>
          }
        </div></div>

        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ 'PlannedItems' | abpLocalization }}</h6>
            @if (status === 0) {
              <button class="btn btn-sm btn-outline-primary" (click)="computeActualDemand()" [disabled]="saving()">
                <i class="fa fa-calculator me-1"></i>{{ 'GetActualDemand' | abpLocalization }}
              </button>
            }
          </div>
          @if ((schedule?.items?.length ?? 0) > 0) {
            <table class="table table-sm mb-0">
              <thead><tr>
                <th>{{ 'Item' | abpLocalization }}</th>
                <th>{{ 'DeliveryDate' | abpLocalization }}</th>
                <th class="text-end">{{ 'PlannedQty' | abpLocalization }}</th>
                <th class="text-end">{{ 'LeadTimeDays' | abpLocalization }}</th>
                <th>{{ 'OrderReleaseDate' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (item of schedule?.items; track item.id) {
                  <tr>
                    <td>{{ item.itemName }}</td>
                    <td>{{ item.deliveryDate | date:'dd/MM/yyyy' }}</td>
                    <td class="text-end" style="max-width:120px">
                      <input type="number" class="form-control form-control-sm text-end" [(ngModel)]="item.plannedQty" [disabled]="status !== 0" />
                    </td>
                    <td class="text-end">{{ item.cumulativeLeadTimeDays }}</td>
                    <td>{{ item.orderReleaseDate | date:'dd/MM/yyyy' }}</td>
                  </tr>
                }
              </tbody>
            </table>
            @if (status === 0) {
              <div class="d-flex justify-content-end mt-2">
                <button class="btn btn-sm btn-primary" (click)="save()" [disabled]="saving()">{{ 'Save' | abpLocalization }}</button>
              </div>
            }
          } @else {
            <p class="text-muted small mb-0">{{ 'NoDemandComputedYet' | abpLocalization }}</p>
          }
        </div></div>
      }
    </abp-page>
  `,
})
export class MasterProductionScheduleFormComponent implements OnInit {
  private service = inject(MasterProductionScheduleService);
  private warehouseService = inject(WarehouseService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = signal(false);
  isEdit = signal(false);
  private scheduleId: string | null = null;
  status = 0;
  schedule: MasterProductionScheduleDto | null = null;

  warehouses = signal<{ id: string; warehouseName: string }[]>([]);

  form: { postingDate: string; fromDate: string; parentWarehouseId: string } = {
    postingDate: new Date().toISOString().substring(0, 10),
    fromDate: new Date().toISOString().substring(0, 10),
    parentWarehouseId: '',
  };

  ngOnInit(): void {
    this.warehouseService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.warehouses.set((r.items ?? []).map((w: any) => ({ id: w.id, warehouseName: w.warehouseName ?? w.name }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.scheduleId = id;
      this.load(id);
    }
  }

  private load(id: string): void {
    this.service.get(id).subscribe(s => {
      this.schedule = s;
      this.status = s.status ?? 0;
      this.form = {
        postingDate: s.postingDate ? s.postingDate.substring(0, 10) : '',
        fromDate: s.fromDate ? s.fromDate.substring(0, 10) : '',
        parentWarehouseId: s.parentWarehouseId ?? '',
      };
    });
  }

  readOnly(): boolean { return this.isEdit() && this.status !== 0; }
  statusLabel(): string { return ['Draft', 'Submitted', '', '', 'Cancelled'][this.status] ?? 'Draft'; }

  save(): void {
    this.saving.set(true);
    if (!this.scheduleId) {
      const dto = {
        companyId: this.companyContext.currentCompanyId(),
        postingDate: this.form.postingDate,
        fromDate: this.form.fromDate,
        parentWarehouseId: this.form.parentWarehouseId || null,
      };
      this.service.create(dto).subscribe({
        next: (r) => {
          this.saving.set(false);
          this.toaster.success('::SuccessfullySaved');
          this.router.navigate(['/manufacturing/master-production-schedules', r.id]);
        },
        error: () => this.saving.set(false),
      });
      return;
    }

    const dto = {
      postingDate: this.form.postingDate,
      fromDate: this.form.fromDate,
      parentWarehouseId: this.form.parentWarehouseId || null,
      items: this.schedule?.items ?? [],
    };
    this.service.update(this.scheduleId, dto).subscribe({
      next: (r) => { this.schedule = r; this.status = r.status ?? 0; this.saving.set(false); this.toaster.success('::SuccessfullyUpdated'); },
      error: () => this.saving.set(false),
    });
  }

  fetchSalesOrders(): void {
    if (!this.scheduleId) return;
    this.saving.set(true);
    this.service.fetchSalesOrders(this.scheduleId, {}).subscribe({
      next: (r) => { this.schedule = r; this.saving.set(false); this.toaster.success('::SuccessfullySaved'); },
      error: () => this.saving.set(false),
    });
  }

  fetchMaterialRequests(): void {
    if (!this.scheduleId) return;
    this.saving.set(true);
    this.service.fetchMaterialRequests(this.scheduleId, {}).subscribe({
      next: (r) => { this.schedule = r; this.saving.set(false); this.toaster.success('::SuccessfullySaved'); },
      error: () => this.saving.set(false),
    });
  }

  computeActualDemand(): void {
    if (!this.scheduleId) return;
    this.saving.set(true);
    this.service.computeActualDemand(this.scheduleId).subscribe({
      next: (r) => { this.schedule = r; this.saving.set(false); this.toaster.success('::SuccessfullySaved'); },
      error: () => this.saving.set(false),
    });
  }

  submit(): void {
    if (!this.scheduleId) return;
    this.saving.set(true);
    this.service.submit(this.scheduleId).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.saving.set(false); this.load(this.scheduleId!); },
      error: () => this.saving.set(false),
    });
  }

  cancelSchedule(): void {
    if (!this.scheduleId) return;
    this.saving.set(true);
    this.service.cancel(this.scheduleId).subscribe({
      next: () => { this.toaster.success('::SuccessfullyCancelled'); this.saving.set(false); this.load(this.scheduleId!); },
      error: () => this.saving.set(false),
    });
  }
}
