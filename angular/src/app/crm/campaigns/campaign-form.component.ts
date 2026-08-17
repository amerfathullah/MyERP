import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CampaignService } from '../../proxy/crm/campaign.service';

@Component({
  selector: 'app-campaign-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(id ? '::Edit' : '::New') | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ '::CampaignName' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="campaignName" maxlength="200">
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'Description' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="description" maxlength="2000">
            </div>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ '::EmailSchedules' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addSchedule()"><i class="fa fa-plus"></i></button>
          </div>
          <table class="table table-sm">
            <thead><tr><th>{{ '::EmailTemplateId' | abpLocalization }}</th><th>{{ '::SendAfterDays' | abpLocalization }}</th><th></th></tr></thead>
            <tbody formArrayName="emailSchedules">
              @for (row of emailSchedules.controls; track $index; let i = $index) {
                <tr [formGroupName]="i">
                  <td><input type="text" class="form-control form-control-sm" formControlName="emailTemplateId" [placeholder]="'::EmailTemplateIdHint' | abpLocalization"></td>
                  <td><input type="number" class="form-control form-control-sm" formControlName="sendAfterDays" min="0"></td>
                  <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeSchedule(i)"><i class="fa fa-trash"></i></button></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/crm/campaigns">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class CampaignFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(CampaignService);
  private toaster = inject(ToasterService);

  id: string | null = null;

  form = this.fb.group({
    campaignName: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    emailSchedules: this.fb.array([]),
  });

  get emailSchedules(): FormArray { return this.form.get('emailSchedules') as FormArray; }

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.service.get(this.id).subscribe({
        next: (r) => {
          this.form.patchValue({ campaignName: r.campaignName, description: r.description ?? '' });
          (r.emailSchedules ?? []).forEach((s) => this.addSchedule(s.emailTemplateId, s.sendAfterDays));
        },
        error: () => {},
      });
    } else {
      this.id = null;
    }
  }

  addSchedule(emailTemplateId = '', sendAfterDays = 0): void {
    this.emailSchedules.push(this.fb.group({
      emailTemplateId: [emailTemplateId, Validators.required],
      sendAfterDays: [sendAfterDays, [Validators.required, Validators.min(0)]],
    }));
  }

  removeSchedule(index: number): void { this.emailSchedules.removeAt(index); }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const input = {
      campaignName: val.campaignName!,
      description: val.description || undefined,
      emailSchedules: (val.emailSchedules ?? []).map((s: any) => ({
        emailTemplateId: s.emailTemplateId, sendAfterDays: Number(s.sendAfterDays),
      })),
    };
    const req$ = this.id ? this.service.update(this.id, input) : this.service.create(input);
    req$.subscribe({
      next: () => {
        this.toaster.success(this.id ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.router.navigate(['/crm/campaigns']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
