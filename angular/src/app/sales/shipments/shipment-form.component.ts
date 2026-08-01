import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ShipmentService } from '../../proxy/crm/shipment.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-shipment-form',
  standalone: true,
  imports: [AutoValidationDirective, SaveShortcutDirective, CommonModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header">
          <h5 class="mb-0"><i class="fas fa-truck me-2"></i>{{ 'NewShipment' | abpLocalization }}</h5>
        </div>
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()" (appSaveShortcut)="save()">
            <div class="row">
              <div class="col-md-6 mb-3">
                <label class="form-label">{{ 'PickupFrom' | abpLocalization }}</label>
                <select class="form-select" formControlName="pickupFromType">
                  <option value="">{{ '::Select' | abpLocalization }}</option>
                  <option value="Company">{{ 'Company' | abpLocalization }}</option>
                  <option value="Supplier">{{ 'Supplier' | abpLocalization }}</option>
                  <option value="Customer">{{ 'Customer' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-6 mb-3">
                <label class="form-label">{{ 'DeliveryTo' | abpLocalization }}</label>
                <select class="form-select" formControlName="deliveryToType">
                  <option value="">{{ '::Select' | abpLocalization }}</option>
                  <option value="Customer">{{ 'Customer' | abpLocalization }}</option>
                  <option value="Company">{{ 'Company' | abpLocalization }}</option>
                  <option value="Supplier">{{ 'Supplier' | abpLocalization }}</option>
                </select>
              </div>
            </div>

            <div class="row">
              <div class="col-md-4 mb-3">
                <label class="form-label">{{ 'PickupDate' | abpLocalization }}</label>
                <input type="date" class="form-control" formControlName="pickupDate" />
              </div>
              <div class="col-md-4 mb-3">
                <label class="form-label">{{ 'Carrier' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="carrier" />
              </div>
              <div class="col-md-4 mb-3">
                <label class="form-label">{{ 'CarrierService' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="carrierService" />
              </div>
            </div>

            <div class="row">
              <div class="col-md-3 mb-3">
                <label class="form-label">{{ 'NetWeight' | abpLocalization }}</label>
                <input type="number" class="form-control" formControlName="totalNetWeight" min="0" step="0.01" />
              </div>
              <div class="col-md-3 mb-3">
                <label class="form-label">{{ 'GrossWeight' | abpLocalization }}</label>
                <input type="number" class="form-control" formControlName="totalGrossWeight" min="0" step="0.01" />
              </div>
              <div class="col-md-2 mb-3">
                <label class="form-label">{{ 'WeightUom' | abpLocalization }}</label>
                <select class="form-select" formControlName="weightUom">
                  <option value="Kg">Kg</option>
                  <option value="Gram">Gram</option>
                  <option value="Lb">Lb</option>
                </select>
              </div>
              <div class="col-md-2 mb-3">
                <label class="form-label">{{ 'ValueOfGoods' | abpLocalization }}</label>
                <input type="number" class="form-control" formControlName="valueOfGoods" min="0" step="0.01" />
              </div>
              <div class="col-md-2 mb-3">
                <label class="form-label">{{ 'Currency' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="currencyCode" maxlength="3" />
              </div>
            </div>

            <div class="mb-3">
              <label class="form-label">{{ 'Notes' | abpLocalization }}</label>
              <textarea class="form-control" formControlName="notes" rows="3"></textarea>
            </div>

            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || saving()">
                <i class="fas fa-save me-1"></i>{{ 'Save' | abpLocalization }}
              </button>
              <a routerLink="/sales/shipments" class="btn btn-outline-secondary">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </div>
  `
})
export class ShipmentFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private shipmentService = inject(ShipmentService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = signal(false);

  form = this.fb.group({
    pickupFromType: [''],
    deliveryToType: [''],
    pickupDate: [''],
    carrier: [''],
    carrierService: [''],
    totalNetWeight: [0],
    totalGrossWeight: [0],
    weightUom: ['Kg'],
    valueOfGoods: [0],
    currencyCode: ['MYR'],
    notes: ['']
  });

  ngOnInit(): void {
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ currencyCode: 'MYR' });
  }

  save(): void {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    const raw = this.form.getRawValue();
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      pickupFromType: raw.pickupFromType || undefined,
      deliveryToType: raw.deliveryToType || undefined,
      pickupDate: raw.pickupDate || undefined,
      carrier: raw.carrier || undefined,
      carrierService: raw.carrierService || undefined,
      totalNetWeight: raw.totalNetWeight || undefined,
      totalGrossWeight: raw.totalGrossWeight || undefined,
      weightUom: raw.weightUom || undefined,
      valueOfGoods: raw.valueOfGoods || undefined,
      currencyCode: raw.currencyCode || 'MYR',
      notes: raw.notes || undefined
    };
    this.shipmentService.create(dto as any).subscribe({
      next: (result) => {
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/sales/shipments', result.id]);
      },
      error: () => this.saving.set(false)
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
