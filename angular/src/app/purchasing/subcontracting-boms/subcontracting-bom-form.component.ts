import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SubcontractingBomService } from '../../proxy/purchasing/subcontracting-bom.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';

@Component({
  selector: 'app-subcontracting-bom-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'Edit' : 'NewSubcontractingBom') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'FinishedGood' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.finishedGoodId" (ngModelChange)="onFinishedGoodChanged()">
              <option value="">--</option>
              @for (i of items(); track i.id) { <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option> }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'FinishedGoodQty' | abpLocalization }}</label>
            <input type="number" class="form-control" min="0.000001" step="0.01" [(ngModel)]="form.finishedGoodQty" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'FinishedGoodBom' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.finishedGoodBomId">
              <option value="">--</option>
              @for (bom of finishedGoodBoms(); track bom.id) { <option [value]="bom.id">{{ bom.bomNumber }}</option> }
            </select>
          </div>
        </div>

        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'ServiceItem' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.serviceItemId">
              <option value="">--</option>
              @for (i of serviceItems(); track i.id) { <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option> }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'ServiceItemQty' | abpLocalization }}</label>
            <input type="number" class="form-control" min="0" step="0.01" [(ngModel)]="form.serviceItemQty" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ConversionFactor' | abpLocalization }}</label>
            <input type="text" class="form-control" [value]="computeConversionFactor() | number:'1.6-6'" disabled />
          </div>
          <div class="col-md-3 d-flex align-items-end">
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="isActive" [(ngModel)]="form.isActive" />
              <label class="form-check-label" for="isActive">{{ 'IsActive' | abpLocalization }}</label>
            </div>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/purchasing/subcontracting-boms">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving() || !canSave()"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class SubcontractingBomFormComponent implements OnInit {
  private service = inject(SubcontractingBomService);
  private itemService = inject(ItemService);
  private manufacturingService = inject(ManufacturingService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  saving = signal(false);
  isEdit = signal(false);
  private bomId: string | null = null;

  items = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  serviceItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  finishedGoodBoms = signal<{ id: string; bomNumber: string }[]>([]);

  form: { isActive: boolean; finishedGoodId: string; finishedGoodQty: number; finishedGoodBomId: string; serviceItemId: string; serviceItemQty: number } = {
    isActive: true, finishedGoodId: '', finishedGoodQty: 1, finishedGoodBomId: '', serviceItemId: '', serviceItemQty: 1,
  };

  computeConversionFactor(): number {
    return this.form.finishedGoodQty > 0 ? this.form.serviceItemQty / this.form.finishedGoodQty : 0;
  }

  ngOnInit(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r => {
      const all = (r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName, maintainStock: i.maintainStock }));
      this.items.set(all.filter((i: any) => i.maintainStock));
      this.serviceItems.set(all.filter((i: any) => !i.maintainStock));
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.bomId = id;
      this.service.get(id).subscribe(b => {
        this.form = {
          isActive: b.isActive!, finishedGoodId: b.finishedGoodId!, finishedGoodQty: b.finishedGoodQty!,
          finishedGoodBomId: b.finishedGoodBomId!, serviceItemId: b.serviceItemId!, serviceItemQty: b.serviceItemQty!,
        };
        this.onFinishedGoodChanged();
      });
    }
  }

  onFinishedGoodChanged(): void {
    if (!this.form.finishedGoodId) { this.finishedGoodBoms.set([]); return; }
    this.manufacturingService.getBomList({ maxResultCount: 100, status: undefined } as any).subscribe(r => {
      this.finishedGoodBoms.set((r.items ?? [])
        .filter((bom: any) => bom.itemId === this.form.finishedGoodId && bom.isActive)
        .map((bom: any) => ({ id: bom.id, bomNumber: bom.bomNumber })));
    });
  }

  canSave(): boolean {
    return !!(this.form.finishedGoodId && this.form.finishedGoodBomId && this.form.serviceItemId && this.form.finishedGoodQty > 0);
  }

  save(): void {
    this.saving.set(true);
    const dto = { ...this.form };
    const req = this.bomId ? this.service.update(this.bomId, dto) : this.service.create(dto);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.toaster.success(this.bomId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        this.router.navigate(['/purchasing/subcontracting-boms']);
      },
      error: () => this.saving.set(false),
    });
  }
}
