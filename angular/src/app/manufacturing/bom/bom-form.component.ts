import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-bom-form',
  standalone: true,
  imports: [AutoValidationDirective, SaveShortcutDirective, CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="isEditMode ? 'EditBOM' : ('NewBOM' | abpLocalization)">
      <form [formGroup]="form" (ngSubmit)="save()" (appSaveShortcut)="save()">
        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-4 mb-3">
                <label class="form-label">{{ 'Item' | abpLocalization }} *</label>
                <select class="form-select" formControlName="itemId">
                  <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                  @for (item of availableItems(); track item.id) {
                    <option [value]="item.id">{{ item.itemCode }} — {{ item.itemName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-4 mb-3">
                <label class="form-label">{{ 'ItemName' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="itemName" />
              </div>
              <div class="col-md-2 mb-3">
                <label class="form-label">{{ 'Quantity' | abpLocalization }} *</label>
                <input type="number" class="form-control" formControlName="quantity" min="0.01" step="0.01" />
              </div>
              <div class="col-md-2 mb-3">
                <label class="form-label">{{ 'IsActive' | abpLocalization }}</label>
                <div class="form-check mt-2">
                  <input type="checkbox" class="form-check-input" formControlName="isActive" id="bomActive" />
                  <label class="form-check-label" for="bomActive">{{ 'Active' | abpLocalization }}</label>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="card-title mb-0"><i class="fa fa-list me-2"></i>{{ 'Materials' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addMaterial()">
              <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
            </button>
          </div>
          <div class="card-body p-0">
            <table class="table table-sm mb-0">
              <thead>
                <tr>
                  <th class="ps-3">{{ 'Item' | abpLocalization }}</th>
                  <th>{{ 'Description' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                  <th class="pe-3"></th>
                </tr>
              </thead>
              <tbody formArrayName="materials">
                @for (mat of materials.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td class="ps-3">
                      <select class="form-select form-select-sm" formControlName="itemId">
                        <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                        @for (item of availableItems(); track item.id) {
                          <option [value]="item.id">{{ item.itemCode }}</option>
                        }
                      </select>
                    </td>
                    <td><input class="form-control form-control-sm" formControlName="description" /></td>
                    <td><input type="number" class="form-control form-control-sm text-end" formControlName="qty" min="0.01" step="0.01" /></td>
                    <td><input type="number" class="form-control form-control-sm text-end" formControlName="rate" min="0" step="0.01" /></td>
                    <td class="text-end font-monospace">{{ (mat.get('qty')?.value * mat.get('rate')?.value) | number:'1.2-2' }}</td>
                    <td class="pe-3"><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeMaterial(i)"><i class="fa fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <div class="d-flex justify-content-between">
          <div class="fw-bold">{{ 'TotalCost' | abpLocalization }}: {{ totalCost | number:'1.2-2' }}
            <span class="text-muted ms-2">({{ 'Material' | abpLocalization }}: {{ materialCost | number:'1.2-2' }} + {{ 'Operations' | abpLocalization }}: {{ operatingCost | number:'1.2-2' }})</span>
          </div>
          <div class="d-flex gap-2">
            <button type="button" class="btn btn-outline-secondary" routerLink="/manufacturing/bom"><i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            <button type="submit" class="btn btn-primary"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          </div>
        </div>

        <!-- Operations Section -->
        <div class="card mt-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0"><i class="fa fa-gears me-2"></i>{{ 'Operations' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addOperation()">
              <i class="fa fa-plus me-1"></i>{{ 'AddOperation' | abpLocalization }}
            </button>
          </div>
          @if (operations.length > 0) {
          <div class="card-body p-0">
            <table class="table table-sm table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th style="width:60px">{{ 'Sequence' | abpLocalization }}</th>
                  <th>{{ 'Operation' | abpLocalization }}</th>
                  <th style="width:100px">{{ 'Time' | abpLocalization }} (min)</th>
                  <th style="width:100px">{{ 'HourRate' | abpLocalization }}</th>
                  <th style="width:90px">{{ 'BatchSize' | abpLocalization }}</th>
                  <th style="width:100px" class="text-end">{{ 'Cost' | abpLocalization }}</th>
                  <th style="width:50px"></th>
                </tr>
              </thead>
              <tbody>
                @for (op of operations.controls; track $index; let i = $index) {
                  <tr [formGroup]="$any(op)">
                    <td><input type="number" class="form-control form-control-sm" formControlName="sequenceId" min="1" /></td>
                    <td><input type="text" class="form-control form-control-sm" formControlName="description" [placeholder]="'OperationName' | abpLocalization" /></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="timeInMins" min="0" step="0.5" (change)="recalcOpCost(i)" /></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="workstationHourRate" min="0" step="1" (change)="recalcOpCost(i)" /></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="batchSize" min="0" /></td>
                    <td class="text-end font-monospace">{{ getOpCost(i) | number:'1.2-2' }}</td>
                    <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeOperation(i)"><i class="fa fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          }
        </div>
      </form>
    </abp-page>
  `,
})
export class BomFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private manufacturingService = inject(ManufacturingService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);

  availableItems = signal<any[]>([]);
  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    companyId: ['', Validators.required],
    itemId: ['', Validators.required],
    itemName: [''],
    quantity: [1, [Validators.required, Validators.min(0.01)]],
    isActive: [true],
    materials: this.fb.array([]),
    operations: this.fb.array([]),
  });

  get materials(): FormArray { return this.form.get('materials') as FormArray; }
  get operations(): FormArray { return this.form.get('operations') as FormArray; }

  get materialCost(): number {
    return this.materials.controls.reduce((sum, c) =>
      sum + (c.get('qty')?.value ?? 0) * (c.get('rate')?.value ?? 0), 0);
  }

  get operatingCost(): number {
    return this.operations.controls.reduce((sum, _, i) => sum + this.getOpCost(i), 0);
  }

  get totalCost(): number { return this.materialCost + this.operatingCost; }

  ngOnInit(): void {
    // Load items for dropdown
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(
      res => this.availableItems.set(res.items ?? []));

    // Auto-fill company from context
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });

    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;
    if (this.isEditMode) {
      this.manufacturingService.getBom(this.entityId!).subscribe(bom => {
        this.form.patchValue({ itemId: bom.itemId, itemName: bom.itemName, quantity: bom.quantity, isActive: bom.isActive });
        (bom.items ?? []).forEach((item: any) => this.addMaterial(item));
        (bom.operations ?? []).forEach((op: any) => this.addOperation(op));
      });
    }
  }

  addMaterial(item?: any): void {
    this.materials.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.itemName ?? ''],
      qty: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      rate: [item?.rate ?? 0, [Validators.required, Validators.min(0)]],
    }));
  }

  removeMaterial(index: number): void { this.materials.removeAt(index); }

  addOperation(op?: any): void {
    const nextSeq = this.operations.length > 0
      ? Math.max(...this.operations.controls.map(c => c.get('sequenceId')?.value ?? 0)) + 10
      : 10;
    this.operations.push(this.fb.group({
      operationId: [op?.operationId ?? ''],
      sequenceId: [op?.sequenceId ?? nextSeq, [Validators.required, Validators.min(1)]],
      description: [op?.description ?? ''],
      timeInMins: [op?.timeInMins ?? 0, [Validators.required, Validators.min(0)]],
      workstationHourRate: [op?.workstationHourRate ?? 0, Validators.min(0)],
      batchSize: [op?.batchSize ?? 0],
      fixedTime: [op?.fixedTime ?? 0],
      isSubcontracted: [op?.isSubcontracted ?? false],
    }));
  }

  removeOperation(index: number): void { this.operations.removeAt(index); }

  getOpCost(index: number): number {
    const op = this.operations.at(index);
    const time = op.get('timeInMins')?.value ?? 0;
    const rate = op.get('workstationHourRate')?.value ?? 0;
    return (time / 60) * rate;
  }

  recalcOpCost(_index: number): void { /* Triggers template re-render via getter */ }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const raw = this.form.getRawValue();
    const payload = {
      ...raw,
      // Map BOM material 'qty'→'quantity' and 'description'→'itemName' to match CreateBomItemDto
      items: this.materials.controls.map(c => {
        const v = c.getRawValue();
        return { itemId: v.itemId, itemName: v.description || '', quantity: v.qty ?? 0, rate: v.rate ?? 0, uom: 'Unit' };
      }),
      operations: this.operations.controls.map(c => c.getRawValue()),
    };
    const req = this.isEditMode
      ? this.manufacturingService.updateBom(this.entityId!, payload as any)
      : this.manufacturingService.createBom(payload as any);
    req.subscribe({
      next: () => { this.toaster.success('BOM saved'); this.router.navigate(['/manufacturing/bom']); },
      error: () => this.toaster.error('Save failed'),
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
