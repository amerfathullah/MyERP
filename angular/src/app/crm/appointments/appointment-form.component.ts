import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AppointmentService } from '../../proxy/crm/appointment.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-appointment-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::New' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ '::Customer' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="customerName" maxlength="200">
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ '::ScheduledTime' | abpLocalization }} *</label>
              <input type="datetime-local" class="form-control" formControlName="scheduledTime">
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'Phone' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="phone" maxlength="30">
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'Email' | abpLocalization }}</label>
              <input type="email" class="form-control" formControlName="email" maxlength="256">
            </div>
            <div class="col-12">
              <label class="form-label">{{ '::Details' | abpLocalization }}</label>
              <textarea class="form-control" formControlName="details" rows="3" maxlength="2000"></textarea>
            </div>
          </div>
        </div></div>
        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/crm/appointments">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class AppointmentFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(AppointmentService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  form = this.fb.group({
    customerName: ['', [Validators.required, Validators.maxLength(200)]],
    scheduledTime: ['', Validators.required],
    phone: [''],
    email: [''],
    details: [''],
  });

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const companyId = this.companyContext.currentCompanyId();
    this.service.create({
      companyId: companyId!,
      customerName: val.customerName!,
      scheduledTime: val.scheduledTime!,
      phone: val.phone || undefined,
      email: val.email || undefined,
      details: val.details || undefined,
      createdThroughPortal: false,
    }).subscribe({
      next: (created) => {
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/crm/appointments', created.id]);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }
}
