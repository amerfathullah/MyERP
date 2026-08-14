import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { DeliveryTripService } from '../../proxy/inventory/delivery-trip.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-delivery-trip-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">
          <i class="bi bi-truck me-2"></i>
          {{ (isEditMode ? 'MyERP::EditDeliveryTrip' : 'MyERP::NewDeliveryTrip') | abpLocalization }}
        </h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::TripNumber' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="tripNumber" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Driver' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="driver" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Vehicle' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="vehicle" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::DepartureTime' | abpLocalization }} *</label>
              <input type="datetime-local" class="form-control" formControlName="departureTime" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::DriverEmail' | abpLocalization }}</label>
              <input type="email" class="form-control" formControlName="driverEmail" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::DistanceUom' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="uom" placeholder="Km" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-12">
              <label class="form-label">{{ 'MyERP::DriverAddress' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="driverAddress" />
            </div>
          </div>

          <!-- Delivery Stops Section -->
          <div class="d-flex justify-content-between align-items-center border-bottom pb-2 mt-4 mb-3">
            <h6 class="mb-0 text-muted">
              <i class="bi bi-geo-alt me-2"></i>{{ 'MyERP::DeliveryStops' | abpLocalization }}
            </h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addStop()">
              <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::AddStop' | abpLocalization }}
            </button>
          </div>

          <div formArrayName="deliveryStops">
            <div class="table-responsive">
              <table class="table table-bordered table-sm align-middle">
                <thead class="table-light">
                  <tr>
                    <th style="width: 50px;">#</th>
                    <th>{{ 'MyERP::Address' | abpLocalization }} *</th>
                    <th>{{ 'MyERP::Customer' | abpLocalization }}</th>
                    <th>{{ 'MyERP::DeliveryNote' | abpLocalization }}</th>
                    <th style="width: 120px;">{{ 'MyERP::Distance' | abpLocalization }}</th>
                    <th style="width: 180px;">{{ 'MyERP::EstimatedArrival' | abpLocalization }}</th>
                    <th style="width: 60px;"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (stop of deliveryStops.controls; track $index) {
                    <tr [formGroupName]="$index">
                      <td class="text-center">{{ $index + 1 }}</td>
                      <td>
                        <input type="text" class="form-control form-control-sm" formControlName="address" />
                      </td>
                      <td>
                        <input type="text" class="form-control form-control-sm" formControlName="customerName" />
                      </td>
                      <td>
                        <input type="text" class="form-control form-control-sm" formControlName="deliveryNoteNumber" />
                      </td>
                      <td>
                        <input type="number" step="0.1" class="form-control form-control-sm" formControlName="distance" />
                      </td>
                      <td>
                        <input type="datetime-local" class="form-control form-control-sm" formControlName="estimatedArrival" />
                      </td>
                      <td class="text-center">
                        <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeStop($index)">
                          <i class="bi bi-trash"></i>
                        </button>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="7" class="text-center text-muted py-3">
                        {{ 'MyERP::NoStopsAdded' | abpLocalization }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-4">
            <a routerLink=".." class="btn btn-secondary btn-sm">
              {{ 'MyERP::Cancel' | abpLocalization }}
            </a>
            <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || isSaving">
              @if (isSaving) {
                <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
              }
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class DeliveryTripFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(DeliveryTripService);
  private readonly companyContext = inject(CompanyContextService);
  private readonly toaster = inject(ToasterService);

  id?: string;
  isEditMode = false;
  isSaving = false;

  form: FormGroup = this.fb.group({
    companyId: ['', Validators.required],
    tripNumber: ['', Validators.required],
    driver: ['', Validators.required],
    vehicle: ['', Validators.required],
    departureTime: [new Date().toISOString().substring(0, 16), Validators.required],
    driverName: [''],
    driverEmail: [''],
    driverAddress: [''],
    uom: ['Km'],
    deliveryStops: this.fb.array([]),
  });

  get deliveryStops(): FormArray {
    return this.form.get('deliveryStops') as FormArray;
  }

  ngOnInit(): void {
    this.id = this.route.snapshot.params['id'];
    this.isEditMode = !!this.id;

    const currentCompanyId = this.companyContext.selectedCompanyId();
    if (currentCompanyId) {
      this.form.patchValue({ companyId: currentCompanyId });
    }

    if (this.isEditMode && this.id) {
      this.service.get(this.id).subscribe((trip) => {
        this.form.patchValue({
          companyId: trip.companyId,
          tripNumber: trip.tripNumber,
          driver: trip.driver,
          driverName: trip.driverName,
          driverEmail: trip.driverEmail,
          driverAddress: trip.driverAddress,
          vehicle: trip.vehicle,
          departureTime: trip.departureTime ? trip.departureTime.substring(0, 16) : '',
          uom: trip.uom || 'Km',
        });

        this.deliveryStops.clear();
        if (trip.deliveryStops) {
          for (const stop of trip.deliveryStops) {
            this.deliveryStops.push(this.fb.group({
              id: [stop.id],
              address: [stop.address, Validators.required],
              customerName: [stop.customerName],
              deliveryNoteNumber: [stop.deliveryNoteNumber],
              distance: [stop.distance],
              estimatedArrival: [stop.estimatedArrival ? stop.estimatedArrival.substring(0, 16) : ''],
            }));
          }
        }
      });
    } else {
      this.addStop();
    }
  }

  addStop(): void {
    this.deliveryStops.push(this.fb.group({
      id: [null],
      address: ['', Validators.required],
      customerName: [''],
      deliveryNoteNumber: [''],
      distance: [0],
      estimatedArrival: [''],
    }));
  }

  removeStop(index: number): void {
    this.deliveryStops.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) return;

    this.isSaving = true;
    const value = this.form.value;

    const request$ = this.isEditMode && this.id
      ? this.service.update(this.id, value)
      : this.service.create(value);

    request$.subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['..'], { relativeTo: this.route });
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      }
    });
  }
}
