import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { VehicleService } from '../../proxy/assets/vehicle.service';
import { DriverService } from '../../proxy/assets/driver.service';
import { vehicleFuelTypeOptions } from '../../proxy/assets/vehicle-fuel-type.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-vehicle-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditVehicle' : 'NewVehicle') | abpLocalization">
      <div class="card"><div class="card-body">
        <h6 class="mb-2">{{ 'Vehicle' | abpLocalization }}</h6>
        <div class="row g-3 mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'LicensePlate' | abpLocalization }} *</label>
            <input class="form-control" [(ngModel)]="form.licensePlate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Make' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.make" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Model' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.model" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ChassisNumber' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.chassisNumber" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Color' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.color" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'FuelType' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.fuelType">
              @for (o of fuelTypeOptions; track o.value) { <option [ngValue]="o.value">{{ o.key }}</option> }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'FuelUom' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.fuelUom" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'LastOdometer' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.lastOdometer" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'CarryingCapacity' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.carryingCapacity" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Wheels' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.wheels" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Doors' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.doors" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'VehicleValue' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.vehicleValue" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'AcquisitionDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.acquisitionDate" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'AssignedDriver' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.driverId">
              <option value="">—</option>
              @for (d of drivers(); track d.id) { <option [value]="d.id">{{ d.fullName }}</option> }
            </select>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Insurance' | abpLocalization }}</h6>
        <div class="row g-3 mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'InsuranceCompany' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.insuranceCompany" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'PolicyNumber' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.policyNumber" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'InsuranceStartDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.insuranceStartDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'InsuranceEndDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.insuranceEndDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'RoadTaxExpiryDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.roadTaxExpiryDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'FitnessCertificateExpiryDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.fitnessCertificateExpiryDate" />
          </div>
        </div>

        <hr />
        <div class="d-flex gap-2">
          <button type="button" class="btn btn-primary" [disabled]="!form.licensePlate || isSaving()" (click)="save()">
            @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
            {{ 'Save' | abpLocalization }}
          </button>
          <a class="btn btn-secondary" routerLink="/assets/vehicles">{{ 'Cancel' | abpLocalization }}</a>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class VehicleFormComponent implements OnInit {
  private service = inject(VehicleService);
  private driverService = inject(DriverService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  fuelTypeOptions = vehicleFuelTypeOptions;

  isEdit = signal(false);
  isSaving = signal(false);
  editId = signal<string | null>(null);
  drivers = signal<{ id: string; fullName: string }[]>([]);

  form: {
    licensePlate: string; make: string; model: string; chassisNumber: string; color: string;
    fuelType: number; fuelUom: string; lastOdometer: number | null; carryingCapacity: number | null;
    wheels: number | null; doors: number | null; vehicleValue: number | null; acquisitionDate: string;
    driverId: string; insuranceCompany: string; policyNumber: string; insuranceStartDate: string;
    insuranceEndDate: string; roadTaxExpiryDate: string; fitnessCertificateExpiryDate: string;
  } = {
    licensePlate: '', make: '', model: '', chassisNumber: '', color: '',
    fuelType: 0, fuelUom: '', lastOdometer: 0, carryingCapacity: null,
    wheels: null, doors: null, vehicleValue: null, acquisitionDate: '',
    driverId: '', insuranceCompany: '', policyNumber: '', insuranceStartDate: '',
    insuranceEndDate: '', roadTaxExpiryDate: '', fitnessCertificateExpiryDate: '',
  };

  ngOnInit(): void {
    this.driverService.getList({ companyId: this.companyContext.currentCompanyId(), maxResultCount: 500 } as any)
      .subscribe(r => this.drivers.set((r.items ?? []).map(d => ({ id: d.id!, fullName: d.fullName ?? '' }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.editId.set(id);
      this.service.get(id).subscribe(v => {
        this.form = {
          licensePlate: v.licensePlate ?? '', make: v.make ?? '', model: v.model ?? '',
          chassisNumber: v.chassisNumber ?? '', color: v.color ?? '',
          fuelType: v.fuelType ?? 0, fuelUom: v.fuelUom ?? '',
          lastOdometer: v.lastOdometer ?? 0, carryingCapacity: v.carryingCapacity ?? null,
          wheels: v.wheels ?? null, doors: v.doors ?? null, vehicleValue: v.vehicleValue ?? null,
          acquisitionDate: v.acquisitionDate ? v.acquisitionDate.substring(0, 10) : '',
          driverId: v.driverId ?? '', insuranceCompany: v.insuranceCompany ?? '', policyNumber: v.policyNumber ?? '',
          insuranceStartDate: v.insuranceStartDate ? v.insuranceStartDate.substring(0, 10) : '',
          insuranceEndDate: v.insuranceEndDate ? v.insuranceEndDate.substring(0, 10) : '',
          roadTaxExpiryDate: v.roadTaxExpiryDate ? v.roadTaxExpiryDate.substring(0, 10) : '',
          fitnessCertificateExpiryDate: v.fitnessCertificateExpiryDate ? v.fitnessCertificateExpiryDate.substring(0, 10) : '',
        };
      });
    }
  }

  save(): void {
    if (!this.form.licensePlate) return;
    this.isSaving.set(true);
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      licensePlate: this.form.licensePlate,
      make: this.form.make || undefined,
      model: this.form.model || undefined,
      chassisNumber: this.form.chassisNumber || undefined,
      color: this.form.color || undefined,
      fuelType: this.form.fuelType,
      fuelUom: this.form.fuelUom || undefined,
      lastOdometer: this.form.lastOdometer ?? 0,
      carryingCapacity: this.form.carryingCapacity ?? undefined,
      wheels: this.form.wheels ?? undefined,
      doors: this.form.doors ?? undefined,
      vehicleValue: this.form.vehicleValue ?? undefined,
      acquisitionDate: this.form.acquisitionDate || undefined,
      driverId: this.form.driverId || undefined,
      insuranceCompany: this.form.insuranceCompany || undefined,
      policyNumber: this.form.policyNumber || undefined,
      insuranceStartDate: this.form.insuranceStartDate || undefined,
      insuranceEndDate: this.form.insuranceEndDate || undefined,
      roadTaxExpiryDate: this.form.roadTaxExpiryDate || undefined,
      fitnessCertificateExpiryDate: this.form.fitnessCertificateExpiryDate || undefined,
    };
    const req$ = this.isEdit() ? this.service.update(this.editId()!, dto) : this.service.create(dto);
    req$.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/vehicles']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
