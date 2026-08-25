import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { ItemLeadTimeService } from '../../proxy/inventory/item-lead-time.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

@Component({
  selector: 'app-item-lead-time-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Item Lead Time</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Item *</label>
              <select class="form-select" formControlName="itemId">
                <option [ngValue]="null">-- Select Item --</option>
                @for (item of itemsList; track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>
          </div>

          <h6 class="border-bottom pb-2 mb-3 mt-4 text-primary">Manufacturing Lead Time</h6>
          <div class="row mb-3">
            <div class="col-md-3">
              <label class="form-label">Shift Time (Hours) *</label>
              <input type="number" class="form-control" formControlName="shiftTimeInHours" (input)="recalculate()">
            </div>
            <div class="col-md-3">
              <label class="form-label">No of Workstations *</label>
              <input type="number" class="form-control" formControlName="noOfWorkstations" (input)="recalculate()">
            </div>
            <div class="col-md-3">
              <label class="form-label">No of Shifts *</label>
              <input type="number" class="form-control" formControlName="noOfShifts" (input)="recalculate()">
            </div>
            <div class="col-md-3">
              <label class="form-label">Total Workstation Time (Hours)</label>
              <input type="number" class="form-control" [value]="calcTotalWorkstationTime" readonly>
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-3">
              <label class="form-label">Manufacturing Time (Mins) *</label>
              <input type="number" class="form-control" formControlName="manufacturingTimeInMins" (input)="recalculate()">
            </div>
            <div class="col-md-3">
              <label class="form-label">Daily Yield (%) *</label>
              <input type="number" step="0.1" class="form-control" formControlName="dailyYield" (input)="recalculate()">
            </div>
            <div class="col-md-3">
              <label class="form-label">Units Produced / Day</label>
              <input type="number" class="form-control" [value]="calcUnitsProduced" readonly>
            </div>
            <div class="col-md-3">
              <label class="form-label">Capacity / Day</label>
              <input type="number" class="form-control fw-bold text-success" [value]="calcCapacityPerDay" readonly>
            </div>
          </div>

          <h6 class="border-bottom pb-2 mb-3 mt-4 text-primary">Purchase Lead Time</h6>
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Purchase Time (Days)</label>
              <input type="number" class="form-control" formControlName="purchaseTimeDays">
            </div>
            <div class="col-md-6">
              <label class="form-label">Buffer Time (Days)</label>
              <input type="number" class="form-control" formControlName="bufferTimeDays">
            </div>
          </div>

          <div class="d-flex justify-content-between align-items-center mb-2 mt-4">
            <h6 class="text-primary mb-0">Supplier-Specific Lead Times</h6>
            <button type="button" class="btn btn-sm btn-outline-secondary" (click)="addSupplierRow()">+ Add Supplier</button>
          </div>

          <table class="table table-bordered table-sm mb-4">
            <thead class="table-light">
              <tr>
                <th>Supplier</th>
                <th style="width: 150px;">Purchase (Days)</th>
                <th style="width: 150px;">Buffer (Days)</th>
                <th style="width: 100px;" class="text-center">Default</th>
                <th style="width: 80px;" class="text-center">Action</th>
              </tr>
            </thead>
            <tbody formArrayName="suppliers">
              @for (row of suppliersArray.controls; track $index; let i = $index) {
                <tr [formGroupName]="i">
                  <td>
                    <select class="form-select form-select-sm" formControlName="supplierId">
                      <option [ngValue]="null">-- Select Supplier --</option>
                      @for (s of suppliersList; track s.id) {
                        <option [value]="s.id">{{ s.name }}</option>
                      }
                    </select>
                  </td>
                  <td>
                    <input type="number" class="form-control form-control-sm" formControlName="purchaseTimeDays">
                  </td>
                  <td>
                    <input type="number" class="form-control form-control-sm" formControlName="bufferTimeDays">
                  </td>
                  <td class="text-center">
                    <input type="checkbox" class="form-check-input" formControlName="isDefault" (change)="onDefaultChanged(i)">
                  </td>
                  <td class="text-center">
                    <button type="button" class="btn btn-sm btn-outline-danger py-0 px-2" (click)="removeSupplierRow(i)">&times;</button>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="5" class="text-center text-muted py-2">No supplier overrides configured.</td>
                </tr>
              }
            </tbody>
          </table>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/inventory/item-lead-times" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ItemLeadTimeFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ItemLeadTimeService);
  private itemService = inject(ItemService);
  private supplierService = inject(SupplierService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;
  itemsList: any[] = [];
  suppliersList: any[] = [];

  calcTotalWorkstationTime = 8;
  calcUnitsProduced = 0;
  calcCapacityPerDay = 0;

  get suppliersArray(): FormArray {
    return this.form.get('suppliers') as FormArray;
  }

  constructor() {
    this.form = this.fb.group({
      itemId: [null, Validators.required],
      shiftTimeInHours: [8, [Validators.required, Validators.min(1), Validators.max(24)]],
      noOfWorkstations: [1, [Validators.required, Validators.min(1)]],
      noOfShifts: [1, [Validators.required, Validators.min(1), Validators.max(10)]],
      manufacturingTimeInMins: [0, [Validators.required, Validators.min(0)]],
      dailyYield: [90.0, [Validators.required, Validators.min(0), Validators.max(100)]],
      purchaseTimeDays: [0, [Validators.required, Validators.min(0)]],
      bufferTimeDays: [0, [Validators.required, Validators.min(0)]],
      suppliers: this.fb.array([]),
    });
  }

  ngOnInit() {
    this.itemService.getList({ maxResultCount: 1000 } as any).subscribe(res => {
      this.itemsList = res.items ?? [];
    });
    this.supplierService.getList({ maxResultCount: 1000 } as any).subscribe(res => {
      this.suppliersList = res.items ?? [];
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue(res);
        this.suppliersArray.clear();
        if (res.suppliers) {
          res.suppliers.forEach(s => {
            this.suppliersArray.push(this.fb.group({
              supplierId: [s.supplierId, Validators.required],
              purchaseTimeDays: [s.purchaseTimeDays, Validators.min(0)],
              bufferTimeDays: [s.bufferTimeDays, Validators.min(0)],
              isDefault: [s.isDefault],
            }));
          });
        }
        this.recalculate();
      });
    } else {
      this.recalculate();
    }
  }

  addSupplierRow() {
    this.suppliersArray.push(this.fb.group({
      supplierId: [null, Validators.required],
      purchaseTimeDays: [0, Validators.min(0)],
      bufferTimeDays: [0, Validators.min(0)],
      isDefault: [false],
    }));
  }

  removeSupplierRow(index: number) {
    this.suppliersArray.removeAt(index);
  }

  onDefaultChanged(index: number) {
    const rows = this.suppliersArray.controls;
    if (rows[index].get('isDefault')?.value) {
      rows.forEach((r, idx) => {
        if (idx !== index) {
          r.get('isDefault')?.setValue(false);
        }
      });
    }
  }

  recalculate() {
    const shift = Number(this.form.get('shiftTimeInHours')?.value) || 0;
    const ws = Number(this.form.get('noOfWorkstations')?.value) || 0;
    const shifts = Number(this.form.get('noOfShifts')?.value) || 0;
    const mfgTime = Number(this.form.get('manufacturingTimeInMins')?.value) || 0;
    const yieldPct = Number(this.form.get('dailyYield')?.value) || 0;

    this.calcTotalWorkstationTime = shift * ws * shifts;
    if (mfgTime > 0) {
      this.calcUnitsProduced = Math.floor((this.calcTotalWorkstationTime * 60) / mfgTime);
      this.calcCapacityPerDay = Math.round((yieldPct * this.calcUnitsProduced) / 100);
    } else {
      this.calcUnitsProduced = 0;
      this.calcCapacityPerDay = 0;
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/inventory/item-lead-times']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
