import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BomCreatorService } from '../../proxy/manufacturing/bom-creator.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-bom-creator-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'Edit' : 'NewBomCreator') | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        @if (statusLabel() !== 'Draft') {
          <div class="alert" [class.alert-success]="statusLabel()==='Completed'" [class.alert-danger]="statusLabel()==='Failed'">
            {{ 'Status' | abpLocalization }}: {{ statusLabel() }}
            @if (errorLog) { <div class="small mt-1">{{ errorLog }}</div> }
          </div>
        }

        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'FinishedGood' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.finishedGoodItemId" [disabled]="statusLabel() !== 'Draft'">
              <option value="">-- {{ 'SelectItem' | abpLocalization }} --</option>
              @for (i of availableItems(); track i.id) { <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option> }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'Quantity' | abpLocalization }}</label>
            <input type="number" class="form-control" min="0.01" step="0.01" [(ngModel)]="form.qty" [disabled]="statusLabel() !== 'Draft'" />
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'UOM' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.uom" [disabled]="statusLabel() !== 'Draft'" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'RateOfMaterialsBasedOn' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.rmCostAsPer" [disabled]="statusLabel() !== 'Draft'">
              <option value="Valuation Rate">Valuation Rate</option>
              <option value="Last Purchase Rate">Last Purchase Rate</option>
              <option value="Price List">Price List</option>
            </select>
          </div>
        </div>

        <h6 class="mb-2">{{ 'SubAssembliesAndRawMaterials' | abpLocalization }}</h6>
        <p class="text-muted small">{{ 'BomCreatorTreeHint' | abpLocalization }}</p>
        <div class="table-responsive">
          <table class="table table-sm">
            <thead><tr>
              <th style="min-width:180px">{{ 'ComponentOf' | abpLocalization }}</th>
              <th style="min-width:220px">{{ 'Item' | abpLocalization }}</th>
              <th>{{ 'IsExpandable' | abpLocalization }}</th>
              <th>{{ 'Qty' | abpLocalization }}</th>
              <th>{{ 'Rate' | abpLocalization }}</th>
              <th>{{ 'UOM' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (row of form.items; track $index) {
                <tr>
                  <td>
                    <select class="form-select form-select-sm" [(ngModel)]="row.fgItemId" [disabled]="statusLabel() !== 'Draft'">
                      @for (o of componentOfOptions(); track o.id) { <option [value]="o.id">{{ o.name }}</option> }
                    </select>
                  </td>
                  <td>
                    <select class="form-select form-select-sm" (change)="setRowItem(row, $any($event.target).value)" [disabled]="statusLabel() !== 'Draft'">
                      <option value="">--</option>
                      @for (i of availableItems(); track i.id) {
                        <option [value]="i.id" [selected]="i.id === row.itemId">{{ i.itemCode }} — {{ i.itemName }}</option>
                      }
                    </select>
                  </td>
                  <td class="text-center"><input type="checkbox" [(ngModel)]="row.isExpandable" [disabled]="statusLabel() !== 'Draft'" /></td>
                  <td><input type="number" class="form-control form-control-sm" [(ngModel)]="row.qty" [disabled]="statusLabel() !== 'Draft'" /></td>
                  <td><input type="number" class="form-control form-control-sm" [(ngModel)]="row.rate" [disabled]="statusLabel() !== 'Draft'" /></td>
                  <td><input class="form-control form-control-sm" [(ngModel)]="row.uom" [disabled]="statusLabel() !== 'Draft'" /></td>
                  <td>
                    @if (statusLabel() === 'Draft') {
                      <button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index,1)"><i class="fa fa-trash"></i></button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        @if (statusLabel() === 'Draft') {
          <button class="btn btn-sm btn-outline-primary mb-3" (click)="addRow()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>
        }

        <div class="mb-3">
          <label class="form-label">{{ 'Remarks' | abpLocalization }}</label>
          <textarea class="form-control" rows="2" [(ngModel)]="form.remarks" [disabled]="statusLabel() !== 'Draft'"></textarea>
        </div>

        <div class="d-flex justify-content-between align-items-center">
          <span class="fw-bold">{{ 'TotalCost' | abpLocalization }}: {{ totalCost() | number:'1.2-2' }}</span>
          <div class="d-flex gap-2">
            <a class="btn btn-secondary" routerLink="/manufacturing/bom-creators">{{ 'Cancel' | abpLocalization }}</a>
            @if (statusLabel() === 'Draft') {
              <button class="btn btn-primary" (click)="save()" [disabled]="saving()"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
            }
            @if (isEdit() && statusLabel() === 'Draft') {
              <button class="btn btn-success" (click)="createBoms()" [disabled]="saving()"><i class="fa fa-sitemap me-1"></i>{{ 'CreateBOMs' | abpLocalization }}</button>
            }
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class BomCreatorFormComponent implements OnInit {
  private service = inject(BomCreatorService);
  private itemService = inject(ItemService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = signal(false);
  isEdit = signal(false);
  private creatorId: string | null = null;
  private status = 0;
  errorLog: string | null = null;

  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);

  form: any = {
    finishedGoodItemId: '', qty: 1, uom: '', rmCostAsPer: 'Valuation Rate', remarks: '',
    items: [] as any[],
  };

  ngOnInit(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.creatorId = id;
      this.load(id);
    }
  }

  private load(id: string): void {
    this.service.get(id).subscribe(c => {
      this.status = c.status ?? 0;
      this.errorLog = c.errorLog ?? null;
      this.form = {
        finishedGoodItemId: c.finishedGoodItemId, qty: c.qty, uom: c.uom ?? '',
        rmCostAsPer: c.rmCostAsPer, remarks: c.remarks ?? '',
        items: (c.items ?? []).map(i => ({ ...i })),
      };
    });
  }

  statusLabel(): string { return ['Draft', 'Completed', 'Failed'][this.status] ?? 'Draft'; }

  componentOfOptions(): { id: string; name: string }[] {
    const fg = this.availableItems().find(i => i.id === this.form.finishedGoodItemId);
    const opts = fg ? [{ id: fg.id, name: `${fg.itemCode} (Top Level)` }] : [];
    for (const row of this.form.items) {
      if (row.isExpandable && row.itemId) {
        const item = this.availableItems().find(i => i.id === row.itemId);
        if (item && !opts.some(o => o.id === item.id)) opts.push({ id: item.id, name: `${item.itemCode} — ${item.itemName}` });
      }
    }
    return opts;
  }

  setRowItem(row: any, itemId: string): void {
    row.itemId = itemId;
    const item = this.availableItems().find(i => i.id === itemId);
    row.itemName = item ? item.itemName : '';
  }

  addRow(): void {
    this.form.items.push({
      itemId: '', itemName: '', fgItemId: this.form.finishedGoodItemId, isExpandable: false,
      qty: 1, rate: 0, uom: '', conversionFactor: 1, stockUom: 'Unit',
    });
  }

  totalCost(): number {
    return this.form.items
      .filter((i: any) => i.fgItemId === this.form.finishedGoodItemId)
      .reduce((s: number, i: any) => s + (i.qty || 0) * (i.rate || 0), 0);
  }

  save(): void {
    this.saving.set(true);
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      finishedGoodItemId: this.form.finishedGoodItemId,
      qty: this.form.qty,
      uom: this.form.uom || null,
      rmCostAsPer: this.form.rmCostAsPer,
      remarks: this.form.remarks || null,
      items: this.form.items.filter((i: any) => i.itemId && i.fgItemId),
    };
    const req = this.creatorId ? this.service.update(this.creatorId, dto) : this.service.create(dto);
    req.subscribe({
      next: (r) => {
        this.saving.set(false);
        this.toaster.success(this.creatorId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        if (!this.creatorId) this.router.navigate(['/manufacturing/bom-creators', r.id]);
      },
      error: () => this.saving.set(false),
    });
  }

  createBoms(): void {
    if (!this.creatorId) return;
    this.saving.set(true);
    this.service.createBoms(this.creatorId).subscribe({
      next: (r) => {
        this.saving.set(false);
        this.status = r.status ?? 1;
        this.errorLog = r.errorLog ?? null;
        if (this.status === 1) {
          this.toaster.success('::SuccessfullyCompleted');
          this.router.navigate(['/manufacturing/bom-creators']);
        } else {
          this.toaster.error(this.errorLog ?? '::AnErrorOccurred');
        }
      },
      error: () => { this.saving.set(false); this.load(this.creatorId!); },
    });
  }
}
