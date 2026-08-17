import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AppointmentBookingSettingsService } from '../../proxy/crm/appointment-booking-settings.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

const DAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

@Component({
  selector: 'app-appointment-booking-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::AppointmentBookingSettings' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-3">
              <label class="form-label">{{ '::AppointmentDurationMinutes' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="appointmentDurationMinutes" min="1">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::AdvanceBookingDays' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="advanceBookingDays" min="0">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::VerificationLinkExpiryMinutes' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="verificationLinkExpiryMinutes" min="15" max="60">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::ActionForExpiredUnverified' | abpLocalization }}</label>
              <select class="form-select" formControlName="actionForExpiredUnverified">
                <option [value]="0">{{ '::NoAction' | abpLocalization }}</option>
                <option [value]="1">{{ '::CancelAppointment' | abpLocalization }}</option>
              </select>
            </div>
          </div>
          <div class="row g-3 mt-1">
            <div class="col-md-4 d-flex align-items-end">
              <div class="form-check">
                <input type="checkbox" class="form-check-input" id="enableScheduling" formControlName="enableScheduling">
                <label class="form-check-label" for="enableScheduling">{{ '::EnableScheduling' | abpLocalization }}</label>
              </div>
            </div>
            <div class="col-md-4 d-flex align-items-end">
              <div class="form-check">
                <input type="checkbox" class="form-check-input" id="enablePortal" formControlName="enableAppointmentPortal">
                <label class="form-check-label" for="enablePortal">{{ '::EnableAppointmentPortal' | abpLocalization }}</label>
              </div>
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ '::ServiceDays' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addWindow()"><i class="fa fa-plus"></i></button>
          </div>
          <table class="table table-sm">
            <thead><tr><th>{{ '::DayOfWeek' | abpLocalization }}</th><th>{{ '::StartTime' | abpLocalization }}</th><th>{{ '::EndTime' | abpLocalization }}</th><th></th></tr></thead>
            <tbody formArrayName="availabilityOfSlots">
              @for (row of availabilityOfSlots.controls; track $index; let i = $index) {
                <tr [formGroupName]="i">
                  <td>
                    <select class="form-select form-select-sm" formControlName="dayOfWeek">
                      @for (day of dayNames; track $index; let d = $index) { <option [value]="d">{{ day }}</option> }
                    </select>
                  </td>
                  <td><input type="time" class="form-control form-control-sm" formControlName="fromTime"></td>
                  <td><input type="time" class="form-control form-control-sm" formControlName="toTime"></td>
                  <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeWindow(i)"><i class="fa fa-trash"></i></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>

        <div class="d-flex justify-content-end gap-2">
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class AppointmentBookingSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(AppointmentBookingSettingsService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  dayNames = DAY_NAMES;

  form = this.fb.group({
    appointmentDurationMinutes: [60],
    advanceBookingDays: [30],
    verificationLinkExpiryMinutes: [15, [Validators.min(15), Validators.max(60)]],
    actionForExpiredUnverified: [1],
    enableScheduling: [false],
    enableAppointmentPortal: [false],
    availabilityOfSlots: this.fb.array([]),
  });

  get availabilityOfSlots(): FormArray { return this.form.get('availabilityOfSlots') as FormArray; }

  ngOnInit(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.service.getForCompany(companyId).subscribe({
      next: (r) => {
        if (!r) return;
        this.form.patchValue({
          appointmentDurationMinutes: r.appointmentDurationMinutes,
          advanceBookingDays: r.advanceBookingDays,
          verificationLinkExpiryMinutes: r.verificationLinkExpiryMinutes,
          actionForExpiredUnverified: r.actionForExpiredUnverified,
          enableScheduling: r.enableScheduling,
          enableAppointmentPortal: r.enableAppointmentPortal,
        });
        (r.availabilityOfSlots ?? []).forEach((w) => this.addWindow(w.dayOfWeek, w.fromTime, w.toTime));
      },
      error: () => {},
    });
  }

  addWindow(dayOfWeek = 1, fromTime = '09:00:00', toTime = '18:00:00'): void {
    this.availabilityOfSlots.push(this.fb.group({
      dayOfWeek: [dayOfWeek],
      fromTime: [fromTime?.substring(0, 5) ?? '09:00'],
      toTime: [toTime?.substring(0, 5) ?? '18:00'],
    }));
  }

  removeWindow(index: number): void { this.availabilityOfSlots.removeAt(index); }

  save(): void {
    if (this.form.invalid) return;
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    const val = this.form.getRawValue();
    this.service.save({
      companyId,
      appointmentDurationMinutes: val.appointmentDurationMinutes!,
      advanceBookingDays: val.advanceBookingDays!,
      verificationLinkExpiryMinutes: val.verificationLinkExpiryMinutes!,
      actionForExpiredUnverified: Number(val.actionForExpiredUnverified),
      enableScheduling: val.enableScheduling ?? false,
      enableAppointmentPortal: val.enableAppointmentPortal ?? false,
      agentUserIds: [],
      availabilityOfSlots: (val.availabilityOfSlots ?? []).map((w: any) => ({
        dayOfWeek: Number(w.dayOfWeek), fromTime: w.fromTime + ':00', toTime: w.toTime + ':00',
      })),
    }).subscribe({
      next: () => this.toaster.success('::SuccessfullyUpdated'),
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
