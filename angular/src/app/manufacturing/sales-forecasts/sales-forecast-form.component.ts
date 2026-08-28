import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesForecastService } from '../../proxy/manufacturing/sales-forecast.service';
import type { SalesForecastDto } from '../../proxy/manufacturing/models';
import { SalesForecastFrequency } from '../../proxy/manufacturing/sales-forecast-frequency.enum';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

/**
 * Sales Forecast form — pick items + a parent warehouse, project demand rows across
 * Weekly/Monthly periods, then (once submitted) spin off a Master Production Schedule.
 * Per ERPNext: generate_demand() button + create_mps() (get_mapped_doc).
 */
@Component({
  selector: 'app-sales-forecast-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? forecast?.forecastNumber : 'NewSalesForecast') | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        @if (isEdit() && status !== 0) {
          <div class="alert" [class.alert-success]="status===1" [class.alert-secondary]="status===4">
            {{ 'Status' | abpLocalization }}: {{ statusLabel() }}
            @if (forecast?.forecastStatus) { — {{ forecast?.forecastStatus }} }
          </div>
        }

        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ 'FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.fromDate" [disabled]="readOnly()" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Frequency' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.frequency" [disabled]="readOnly()">
              <option [ngValue]="0">Weekly</option>
              <option [ngValue]="1">Monthly</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'NumberOfWeeksMonths' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.demandNumber" [disabled]="readOnly()" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ParentWarehouse' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.parentWarehouseId" [disabled]="readOnly()">
              <option value="">--</option>
              @for (w of warehouses(); track w.id) { <option [value]="w.id">{{ w.warehouseName }}</option> }
            </select>
          </div>
        </div>

        <label class="form-label">{{ 'SelectItems' | abpLocalization }}</label>
        <div class="border rounded p-2 mb-3" style="max-height:220px; overflow-y:auto;">
          @for (i of items(); track i.id) {
            <div class="form-check">
              <input class="form-check-input" type="checkbox"
                [id]="'item-' + i.id"
                [checked]="selectedItemIds().has(i.id)"
                [disabled]="readOnly()"
                (change)="toggleItem(i.id)" />
              <label class="form-check-label" [for]="'item-' + i.id">{{ i.itemCode }} - {{ i.itemName }}</label>
            </div>
          }
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/manufacturing/sales-forecasts">{{ 'Cancel' | abpLocalization }}</a>
          @if (!readOnly()) {
            <button class="btn btn-primary" (click)="save()" [disabled]="saving()"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          }
          @if (isEdit() && status === 0) {
            <button class="btn btn-outline-primary" (click)="generateDemand()" [disabled]="saving()"><i class="fa fa-calculator me-1"></i>{{ 'GenerateDemand' | abpLocalization }}</button>
            <button class="btn btn-success" (click)="submit()" [disabled]="saving()"><i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}</button>
          }
          @if (isEdit() && status === 1 && forecast?.forecastStatus !== 'MpsGenerated') {
            <button class="btn btn-outline-danger" (click)="cancelForecast()" [disabled]="saving()"><i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            <button class="btn btn-primary" (click)="createMps()" [disabled]="saving()"><i class="fa fa-calendar-days me-1"></i>{{ 'CreateMPS' | abpLocalization }}</button>
          }
        </div>
      </div></div>

      @if (isEdit()) {
        <div class="card"><div class="card-body">
          <h6 class="mb-2">{{ 'DemandRows' | abpLocalization }} ({{ forecast?.items?.length ?? 0 }})</h6>
          @if ((forecast?.items?.length ?? 0) > 0) {
            <table class="table table-sm mb-0">
              <thead><tr>
                <th>{{ 'Item' | abpLocalization }}</th>
                <th>{{ 'DeliveryDate' | abpLocalization }}</th>
                <th class="text-end">{{ 'DemandQty' | abpLocalization }}</th>
                <th>{{ 'UOM' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (item of forecast?.items; track item.id) {
                  <tr>
                    <td>{{ item.itemName }}</td>
                    <td>{{ item.deliveryDate | date:'dd/MM/yyyy' }}</td>
                    <td class="text-end">{{ item.demandQty }}</td>
                    <td>{{ item.uom }}</td>
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="text-muted small mb-0">{{ 'NoDemandComputedYet' | abpLocalization }}</p>
          }
        </div></div>
      }
    </abp-page>
  `,
})
export class SalesForecastFormComponent implements OnInit {
  private service = inject(SalesForecastService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = signal(false);
  isEdit = signal(false);
  private forecastId: string | null = null;
  status = 0;
  forecast: SalesForecastDto | null = null;

  warehouses = signal<{ id: string; warehouseName: string }[]>([]);
  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  selectedItemIds = signal<Set<string>>(new Set());

  form: { fromDate: string; frequency: SalesForecastFrequency; demandNumber: number; parentWarehouseId: string } = {
    fromDate: new Date().toISOString().substring(0, 10),
    frequency: SalesForecastFrequency.Monthly,
    demandNumber: 6,
    parentWarehouseId: '',
  };

  ngOnInit(): void {
    this.warehouseService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.warehouses.set((r.items ?? []).map((w: any) => ({ id: w.id, warehouseName: w.warehouseName ?? w.name }))));
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.items.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.forecastId = id;
      this.load(id);
    }
  }

  private load(id: string): void {
    this.service.get(id).subscribe(f => {
      this.forecast = f;
      this.status = f.status ?? 0;
      this.form = {
        fromDate: f.fromDate ? f.fromDate.substring(0, 10) : '',
        frequency: (f.frequency === 'Weekly' ? 0 : 1) as SalesForecastFrequency,
        demandNumber: f.demandNumber ?? 6,
        parentWarehouseId: f.parentWarehouseId ?? '',
      };
      this.selectedItemIds.set(new Set(f.selectedItemIds ?? []));
    });
  }

  readOnly(): boolean { return this.isEdit() && this.status !== 0; }
  statusLabel(): string { return ['Draft', 'Submitted', '', '', 'Cancelled'][this.status] ?? 'Draft'; }

  toggleItem(id: string): void {
    const set = new Set(this.selectedItemIds());
    if (set.has(id)) set.delete(id); else set.add(id);
    this.selectedItemIds.set(set);
  }

  save(): void {
    this.saving.set(true);
    const selectedItemIds = Array.from(this.selectedItemIds());

    if (!this.forecastId) {
      const dto = {
        companyId: this.companyContext.currentCompanyId(),
        fromDate: this.form.fromDate,
        frequency: this.form.frequency,
        demandNumber: this.form.demandNumber,
        parentWarehouseId: this.form.parentWarehouseId || null,
        selectedItemIds,
      };
      this.service.create(dto as any).subscribe({
        next: (r) => {
          this.saving.set(false);
          this.toaster.success('::SuccessfullySaved');
          this.router.navigate(['/manufacturing/sales-forecasts', r.id]);
        },
        error: () => this.saving.set(false),
      });
      return;
    }

    const dto = {
      fromDate: this.form.fromDate,
      frequency: this.form.frequency,
      demandNumber: this.form.demandNumber,
      parentWarehouseId: this.form.parentWarehouseId || null,
      selectedItemIds,
    };
    this.service.update(this.forecastId, dto as any).subscribe({
      next: (r) => { this.forecast = r; this.status = r.status ?? 0; this.saving.set(false); this.toaster.success('::SuccessfullyUpdated'); },
      error: () => this.saving.set(false),
    });
  }

  generateDemand(): void {
    if (!this.forecastId) return;
    this.saving.set(true);
    this.service.generateDemand(this.forecastId).subscribe({
      next: (r) => { this.forecast = r; this.saving.set(false); this.toaster.success('::SuccessfullySaved'); },
      error: () => this.saving.set(false),
    });
  }

  submit(): void {
    if (!this.forecastId) return;
    this.saving.set(true);
    this.service.submit(this.forecastId).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.saving.set(false); this.load(this.forecastId!); },
      error: () => this.saving.set(false),
    });
  }

  cancelForecast(): void {
    if (!this.forecastId) return;
    this.saving.set(true);
    this.service.cancel(this.forecastId).subscribe({
      next: () => { this.toaster.success('::SuccessfullyCancelled'); this.saving.set(false); this.load(this.forecastId!); },
      error: () => this.saving.set(false),
    });
  }

  createMps(): void {
    if (!this.forecastId) return;
    this.saving.set(true);
    this.service.createMps(this.forecastId).subscribe({
      next: (mpsId) => {
        this.saving.set(false);
        this.toaster.success('::SuccessfullySaved');
        // responseType:'text' on this endpoint returns the raw JSON-quoted string, not a parsed value
        this.router.navigate(['/manufacturing/master-production-schedules', mpsId.replace(/^"|"$/g, '')]);
      },
      error: () => this.saving.set(false),
    });
  }
}
