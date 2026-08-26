import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { CrmSettingsService } from '../../proxy/crm/crm-settings.service';

@Component({
  selector: 'app-crm-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">CRM Settings</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <h6 class="text-secondary mb-3">Lead Settings</h6>
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Campaign Naming By</label>
              <select class="form-select" formControlName="campaignNamingBy">
                <option value="Campaign Name">Campaign Name</option>
                <option value="Naming Series">Naming Series</option>
              </select>
            </div>
            <div class="col-md-6 d-flex align-items-center mt-4">
              <div class="form-check form-switch me-4">
                <input class="form-check-input" type="checkbox" id="allowLeadDuplication" formControlName="allowLeadDuplicationBasedOnEmails">
                <label class="form-check-label" for="allowLeadDuplication">Allow Lead Duplication Based on Emails</label>
              </div>
              <div class="form-check form-switch">
                <input class="form-check-input" type="checkbox" id="autoCreationOfContact" formControlName="autoCreationOfContact">
                <label class="form-check-label" for="autoCreationOfContact">Auto Creation of Contact</label>
              </div>
            </div>
          </div>

          <hr>
          <h6 class="text-secondary mb-3">Opportunity & Quotation Settings</h6>
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Close Replied Opportunity After (Days)</label>
              <input type="number" class="form-control" formControlName="closeOpportunityAfterDays">
            </div>
            <div class="col-md-6">
              <label class="form-label">Default Quotation Validity (Days)</label>
              <input type="number" class="form-control" formControlName="defaultQuotationValidityDays">
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-12">
              <div class="form-check form-switch mb-2">
                <input class="form-check-input" type="checkbox" id="enableOpportunityCreationFromContactUs" formControlName="enableOpportunityCreationFromContactUs">
                <label class="form-check-label" for="enableOpportunityCreationFromContactUs">Enable Opportunity Creation from Contact Us</label>
              </div>
            </div>
          </div>

          <hr>
          <h6 class="text-secondary mb-3">Activity & Communication Settings</h6>
          <div class="row mb-4">
            <div class="col-md-6">
              <div class="form-check form-switch mb-2">
                <input class="form-check-input" type="checkbox" id="carryForward" formControlName="carryForwardCommunicationAndComments">
                <label class="form-check-label" for="carryForward">Carry Forward Communication and Comments (Lead → Opportunity → Quotation)</label>
              </div>
            </div>
            <div class="col-md-6">
              <div class="form-check form-switch mb-2">
                <input class="form-check-input" type="checkbox" id="updateTimestamp" formControlName="updateTimestampOnNewCommunication">
                <label class="form-check-label" for="updateTimestamp">Update Timestamp on New Communication</label>
              </div>
            </div>
          </div>

          <button type="submit" class="btn btn-primary">Save Settings</button>
        </form>
      </div>
    </div>
  `
})
export class CrmSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(CrmSettingsService);
  private toaster = inject(ToasterService);

  form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      campaignNamingBy: ['Campaign Name'],
      allowLeadDuplicationBasedOnEmails: [false],
      autoCreationOfContact: [true],
      closeOpportunityAfterDays: [15],
      enableOpportunityCreationFromContactUs: [false],
      defaultQuotationValidityDays: [30],
      carryForwardCommunicationAndComments: [false],
      updateTimestampOnNewCommunication: [false],
    });
  }

  ngOnInit() {
    this.service.get().subscribe(res => {
      this.form.patchValue(res);
    });
  }

  save() {
    this.service.update(this.form.value).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
