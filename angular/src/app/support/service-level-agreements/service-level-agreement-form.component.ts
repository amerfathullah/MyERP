import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ServiceLevelAgreementService } from '../../proxy/support/service-level-agreement.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

const DAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

@Component({
  selector: 'app-service-level-agreement-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(id ? '::Edit' : '::New') | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'Name' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="name" maxlength="100">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::EntityType' | abpLocalization }}</label>
              <select class="form-select" formControlName="entityType">
                <option value="">{{ '::CompanyDefault' | abpLocalization }}</option>
                <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3 d-flex align-items-end">
              <div class="form-check">
                <input type="checkbox" class="form-check-input" id="isDefault" formControlName="isDefault">
                <label class="form-check-label" for="isDefault">{{ '::SetAsCompanyDefault' | abpLocalization }}</label>
              </div>
            </div>
          </div>
          <div class="row g-3 mt-1">
            <div class="col-md-3">
              <label class="form-label">{{ '::ResponseTimeHours' | abpLocalization }} *</label>
              <input type="number" class="form-control" formControlName="responseTimeHours" min="0">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::ResolutionTimeHours' | abpLocalization }} *</label>
              <input type="number" class="form-control" formControlName="resolutionTimeHours" min="0">
            </div>
            <div class="col-md-3 d-flex align-items-end">
              <div class="form-check">
                <input type="checkbox" class="form-check-input" id="applyOnResolution" formControlName="applyOnResolution">
                <label class="form-check-label" for="applyOnResolution">{{ '::PauseOnHold' | abpLocalization }}</label>
              </div>
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ '::PriorityTargets' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addPriority()"><i class="fa fa-plus"></i></button>
          </div>
          <table class="table table-sm">
            <thead><tr><th>{{ '::PriorityName' | abpLocalization }}</th><th>{{ '::ResponseTimeHours' | abpLocalization }}</th><th>{{ '::ResolutionTimeHours' | abpLocalization }}</th><th></th></tr></thead>
            <tbody formArrayName="priorities">
              @for (row of priorities.controls; track $index; let i = $index) {
                <tr [formGroupName]="i">
                  <td><input type="text" class="form-control form-control-sm" formControlName="priorityName" placeholder="Low / Medium / High / Urgent"></td>
                  <td><input type="number" class="form-control form-control-sm" formControlName="responseTimeHours" min="0"></td>
                  <td><input type="number" class="form-control form-control-sm" formControlName="resolutionTimeHours" min="0"></td>
                  <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removePriority(i)"><i class="fa fa-trash"></i></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ '::ServiceDays' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addServiceDay()"><i class="fa fa-plus"></i></button>
          </div>
          <table class="table table-sm">
            <thead><tr><th>{{ '::DayOfWeek' | abpLocalization }}</th><th>{{ '::StartTime' | abpLocalization }}</th><th>{{ '::EndTime' | abpLocalization }}</th><th></th></tr></thead>
            <tbody formArrayName="serviceDays">
              @for (row of serviceDays.controls; track $index; let i = $index) {
                <tr [formGroupName]="i">
                  <td>
                    <select class="form-select form-select-sm" formControlName="dayOfWeek">
                      @for (day of dayNames; track $index; let d = $index) { <option [value]="d">{{ day }}</option> }
                    </select>
                  </td>
                  <td><input type="time" class="form-control form-control-sm" formControlName="startTime"></td>
                  <td><input type="time" class="form-control form-control-sm" formControlName="endTime"></td>
                  <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeServiceDay(i)"><i class="fa fa-trash"></i></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/support/service-level-agreements">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class ServiceLevelAgreementFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(ServiceLevelAgreementService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  id: string | null = null;
  dayNames = DAY_NAMES;

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    entityType: [''],
    isDefault: [false],
    responseTimeHours: [24, Validators.required],
    resolutionTimeHours: [72, Validators.required],
    applyOnResolution: [true],
    priorities: this.fb.array([]),
    serviceDays: this.fb.array([]),
  });

  get priorities(): FormArray { return this.form.get('priorities') as FormArray; }
  get serviceDays(): FormArray { return this.form.get('serviceDays') as FormArray; }

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.service.get(this.id).subscribe({
        next: (r) => {
          this.form.patchValue({
            name: r.name, entityType: r.entityType ?? '', isDefault: r.isDefault,
            responseTimeHours: r.responseTimeHours, resolutionTimeHours: r.resolutionTimeHours,
            applyOnResolution: r.applyOnResolution,
          });
          (r.priorities ?? []).forEach((p) => this.addPriority(p.priorityName, p.responseTimeHours, p.resolutionTimeHours));
          (r.serviceDays ?? []).forEach((d) => this.addServiceDay(d.dayOfWeek, d.startTime, d.endTime));
        },
        error: () => {},
      });
    } else {
      this.id = null;
    }
  }

  addPriority(name = '', responseHours: number | undefined = undefined, resolutionHours: number | undefined = undefined): void {
    this.priorities.push(this.fb.group({
      priorityName: [name, Validators.required],
      responseTimeHours: [responseHours ?? 24],
      resolutionTimeHours: [resolutionHours ?? 72],
    }));
  }

  removePriority(index: number): void { this.priorities.removeAt(index); }

  addServiceDay(dayOfWeek = 1, startTime = '09:00:00', endTime = '18:00:00'): void {
    this.serviceDays.push(this.fb.group({
      dayOfWeek: [dayOfWeek],
      startTime: [startTime?.substring(0, 5) ?? '09:00'],
      endTime: [endTime?.substring(0, 5) ?? '18:00'],
    }));
  }

  removeServiceDay(index: number): void { this.serviceDays.removeAt(index); }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const companyId = this.companyContext.currentCompanyId();
    const input = {
      companyId: companyId!,
      name: val.name!,
      entityType: val.entityType || undefined,
      isDefault: val.isDefault ?? false,
      responseTimeHours: val.responseTimeHours!,
      resolutionTimeHours: val.resolutionTimeHours!,
      applyOnResolution: val.applyOnResolution ?? true,
      priorities: (val.priorities ?? []).map((p: any) => ({
        priorityName: p.priorityName, responseTimeHours: p.responseTimeHours, resolutionTimeHours: p.resolutionTimeHours,
      })),
      serviceDays: (val.serviceDays ?? []).map((d: any) => ({
        dayOfWeek: Number(d.dayOfWeek), startTime: d.startTime + ':00', endTime: d.endTime + ':00',
      })),
    };
    const req$ = this.id ? this.service.update(this.id, input) : this.service.create(input);
    req$.subscribe({
      next: () => {
        this.toaster.success(this.id ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.router.navigate(['/support/service-level-agreements']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
