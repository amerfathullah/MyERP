import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface TransferLine {
  itemId: string;
  quantity: number;
  valuationRate: number | null;
}

@Component({
  selector: 'app-transit-transfer-new',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-paper-plane me-2"></i>{{ '::NewTransfer' | abpLocalization }}</h5>
          <a routerLink="/inventory/transit-transfers" class="btn btn-sm btn-outline-secondary">
            <i class="fa fa-arrow-left me-1"></i>{{ 'Cancel' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          <div class="row g-3 mb-3">
            <div class="col-md-3">
              <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="postingDate" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ '::SourceWarehouse' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="sourceWarehouseId">
                <option value="">— {{ '::SelectWarehouse' | abpLocalization }} —</option>
                @for (wh of warehouses(); track wh.id) {
                  <option [value]="wh.id">{{ wh.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ '::DestinationWarehouse' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="destinationWarehouseId">
                <option value="">— {{ '::SelectWarehouse' | abpLocalization }} —</option>
                @for (wh of warehouses(); track wh.id) {
                  <option [value]="wh.id">{{ wh.name }}</option>
                }
              </select>
            </div>
          </div>
          @if (sourceWarehouseId && destinationWarehouseId && sourceWarehouseId === destinationWarehouseId) {
            <div class="alert alert-danger py-2">{{ 'SameWarehouseTransfer' | abpLocalization }}</div>
          }

          <h6 class="mb-2">{{ '::Items' | abpLocalization }}</h6>
          <table class="table table-sm">
            <thead><tr>
              <th>{{ 'Item' | abpLocalization }}</th>
              <th>{{ '::TotalQty' | abpLocalization }}</th>
              <th>{{ 'ValuationRate' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (line of lines; track $index) {
                <tr>
                  <td>
                    <select class="form-select form-select-sm" [(ngModel)]="line.itemId">
                      <option value="">-- {{ 'SelectItem' | abpLocalization }} --</option>
                      @for (i of availableItems(); track i.id) {
                        <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option>
                      }
                    </select>
                  </td>
                  <td><input type="number" class="form-control form-control-sm" min="0" [(ngModel)]="line.quantity" /></td>
                  <td><input type="number" class="form-control form-control-sm" min="0" step="0.01" [(ngModel)]="line.valuationRate" /></td>
                  <td><button class="btn btn-sm btn-outline-danger" (click)="lines.splice($index,1)"><i class="fa fa-trash"></i></button></td>
                </tr>
              }
            </tbody>
          </table>
          <button class="btn btn-sm btn-outline-primary mb-3" (click)="addLine()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

          <div class="col-md-4">
            <label class="form-label">{{ 'Notes' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="notes" />
          </div>

          <div class="d-flex justify-content-end mt-3">
            <button class="btn btn-primary" [disabled]="saving || !canSave()" (click)="save()">
              @if (saving) { <span class="spinner-border spinner-border-sm me-1"></span> }
              <i class="fa fa-paper-plane me-1"></i>{{ '::NewTransfer' | abpLocalization }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class TransitTransferNewComponent implements OnInit {
  private stockEntryService = inject(StockEntryService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);

  warehouses = signal<{ id: string; name: string }[]>([]);
  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  saving = false;

  postingDate = new Date().toISOString().split('T')[0];
  sourceWarehouseId = '';
  destinationWarehouseId = '';
  notes = '';
  lines: TransferLine[] = [{ itemId: '', quantity: 0, valuationRate: null }];

  ngOnInit(): void {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: (res: any) => this.warehouses.set(
        (res.items ?? []).filter((w: any) => !w.isGroup).map((w: any) => ({ id: w.id, name: w.warehouseName ?? w.name ?? w.id }))
      ),
      error: () => {},
    });
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe((r: any) =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName })))
    );
  }

  addLine(): void { this.lines.push({ itemId: '', quantity: 0, valuationRate: null }); }

  canSave(): boolean {
    return !!this.sourceWarehouseId && !!this.destinationWarehouseId
      && this.sourceWarehouseId !== this.destinationWarehouseId
      && this.lines.some(l => l.itemId && l.quantity > 0);
  }

  save(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.canSave()) return;

    this.saving = true;
    this.stockEntryService.createTransitTransfer({
      companyId,
      sourceWarehouseId: this.sourceWarehouseId,
      destinationWarehouseId: this.destinationWarehouseId,
      postingDate: this.postingDate,
      notes: this.notes || null,
      items: this.lines
        .filter(l => l.itemId && l.quantity > 0)
        .map(l => ({ itemId: l.itemId, quantity: l.quantity, valuationRate: l.valuationRate })),
    }).subscribe({
      next: () => {
        this.saving = false;
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/inventory/transit-transfers']);
      },
      error: (err: any) => {
        this.saving = false;
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }
}
