import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { EmailCampaignService } from '../../proxy/crm/email-campaign.service';
import { CampaignService } from '../../proxy/crm/campaign.service';
import { LeadService } from '../../proxy/crm/lead.service';

@Component({
  selector: 'app-email-campaign-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::New' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ '::Campaign' | abpLocalization }} *</label>
              <select class="form-select" formControlName="campaignId">
                <option value="">-- {{ 'Select' | abpLocalization }} --</option>
                @for (c of campaigns(); track c.id) { <option [value]="c.id">{{ c.campaignName }}</option> }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::EmailCampaignFor' | abpLocalization }}</label>
              <select class="form-select" formControlName="emailCampaignFor">
                <option [value]="0">{{ '::Lead' | abpLocalization }}</option>
                <option [value]="1">{{ '::Contact' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::StartDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="startDate">
            </div>
          </div>
          <div class="row g-3 mt-1">
            <div class="col-md-6">
              <label class="form-label">{{ '::Recipient' | abpLocalization }} *</label>
              @if (form.value.emailCampaignFor === 0) {
                <select class="form-select" formControlName="recipientId">
                  <option value="">-- {{ 'Select' | abpLocalization }} --</option>
                  @for (l of leads(); track l.id) { <option [value]="l.id">{{ l.fullName ?? l.firstName }}</option> }
                </select>
              } @else {
                <input type="text" class="form-control" formControlName="recipientId" [placeholder]="'::ContactIdHint' | abpLocalization">
              }
            </div>
          </div>
        </div></div>
        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/crm/email-campaigns">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class EmailCampaignFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(EmailCampaignService);
  private campaignService = inject(CampaignService);
  private leadService = inject(LeadService);
  private toaster = inject(ToasterService);

  campaigns = signal<any[]>([]);
  leads = signal<any[]>([]);

  form = this.fb.group({
    campaignId: ['', Validators.required],
    emailCampaignFor: [0],
    recipientId: ['', Validators.required],
    startDate: [new Date().toISOString().substring(0, 10), Validators.required],
  });

  ngOnInit(): void {
    this.campaignService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe({ next: (r) => this.campaigns.set(r.items ?? []), error: () => {} });
    this.leadService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any)
      .subscribe({ next: (r) => this.leads.set(r.items ?? []), error: () => {} });
  }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    this.service.create({
      campaignId: val.campaignId!,
      emailCampaignFor: Number(val.emailCampaignFor),
      recipientId: val.recipientId!,
      startDate: val.startDate!,
    }).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/crm/email-campaigns']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }
}
