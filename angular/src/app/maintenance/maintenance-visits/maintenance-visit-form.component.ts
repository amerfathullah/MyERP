import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-maintenance-visit-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">{{ (isEditMode ? 'MyERP::EditMaintenanceVisit' : 'MyERP::NewMaintenanceVisit') | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::VisitDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="visitDate" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::MaintenanceType' | abpLocalization }} *</label>
              <select class="form-select" formControlName="maintenanceType">
                <option value="Scheduled">Scheduled</option>
                <option value="Unscheduled">Unscheduled</option>
                <option value="Breakdown">Breakdown</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Customer' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="customerId" placeholder="Customer ID" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::MaintenanceSchedule' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="maintenanceScheduleId" placeholder="Schedule ID (optional)" />
            </div>
          </div>

          <h6 class="mt-4 mb-2">{{ 'MyERP::WorkItems' | abpLocalization }}</h6>
          <div class="table-responsive">
            <table class="table table-sm table-bordered">
              <thead class="table-light">
                <tr>
                  <th>{{ 'MyERP::ItemName' | abpLocalization }}</th>
                  <th>{{ 'MyERP::WorkDone' | abpLocalization }} *</th>
                  <th>{{ 'MyERP::Details' | abpLocalization }}</th>
                  <th style="width: 50px;"></th>
                </tr>
              </thead>
              <tbody formArrayName="purposes">
                @for (purpose of purposes.controls; track $index) {
                  <tr [formGroupName]="$index">
                    <td><input type="text" class="form-control form-control-sm" formControlName="itemName" /></td>
                    <td><input type="text" class="form-control form-control-sm" formControlName="workDone" /></td>
                    <td><input type="text" class="form-control form-control-sm" formControlName="workDetails" /></td>
                    <td>
                      <button type="button" class="btn btn-sm btn-outline-danger" (click)="removePurpose($index)">
                        <i class="bi bi-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <button type="button" class="btn btn-sm btn-outline-secondary mb-3" (click)="addPurpose()">
            <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::AddRow' | abpLocalization }}
          </button>

          <div class="d-flex justify-content-end gap-2">
            <a routerLink=".." class="btn btn-secondary">{{ 'MyERP::Cancel' | abpLocalization }}</a>
            <button type="submit" class="btn btn-primary" [disabled]="!form.valid || saving">
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class MaintenanceVisitFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(MaintenanceService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  form!: FormGroup;
  saving = false;
  isEditMode = false;
  editId: string | null = null;

  get purposes(): FormArray {
    return this.form.get('purposes') as FormArray;
  }

  ngOnInit() {
    this.form = this.fb.group({
      visitDate: [new Date().toISOString().substring(0, 10), Validators.required],
      maintenanceType: ['Scheduled', Validators.required],
      customerId: [null],
      maintenanceScheduleId: [null],
      purposes: this.fb.array([]),
    });

    this.editId = this.route.snapshot.paramMap.get('id');
    if (this.editId) {
      this.isEditMode = true;
      this.service.getVisit(this.editId).subscribe({
        next: (v) => {
          this.form.patchValue({
            visitDate: v.visitDate?.substring(0, 10),
            maintenanceType: v.maintenanceType,
            customerId: v.customerId,
            maintenanceScheduleId: v.maintenanceScheduleId,
          });
          (v.purposes ?? []).forEach((p: any) => this.addPurpose(p));
        },
      });
    } else {
      this.addPurpose();
    }
  }

  addPurpose(data?: any) {
    this.purposes.push(this.fb.group({
      itemName: [data?.itemName || ''],
      workDone: [data?.workDone || '', Validators.required],
      workDetails: [data?.workDetails || ''],
      itemId: [data?.itemId || null],
      serialNoId: [data?.serialNoId || null],
    }));
  }

  removePurpose(idx: number) {
    this.purposes.removeAt(idx);
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;

    const payload = {
      ...this.form.value,
      companyId: this.companyContext.currentCompanyId,
      purposes: this.purposes.value.map((p: any) => ({
        itemName: p.itemName,
        workDone: p.workDone,
        workDetails: p.workDetails,
        itemId: p.itemId || undefined,
        serialNoId: p.serialNoId || undefined,
      })),
    };

    const action$ = this.isEditMode
      ? this.service.updateVisit(this.editId!, payload)
      : this.service.createVisit(payload);

    action$.subscribe({
      next: () => {
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['../'], { relativeTo: this.route });
      },
      error: () => { this.saving = false; },
    });
  }
}
