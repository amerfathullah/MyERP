import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { IssueService } from '../../proxy/support/issue.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-issue-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewIssue' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3">
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-8">
                <label class="form-label">{{ 'Subject' | abpLocalization }} *</label>
                <input type="text" class="form-control" formControlName="subject" maxlength="500">
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'Company' | abpLocalization }} *</label>
                <input type="text" class="form-control" formControlName="companyId">
              </div>
            </div>
            <div class="row g-3 mt-2">
              <div class="col-md-4">
                <label class="form-label">{{ 'Priority' | abpLocalization }}</label>
                <select class="form-select" formControlName="priority">
                  <option value="Low">{{ 'Low' | abpLocalization }}</option>
                  <option value="Medium">{{ 'Medium' | abpLocalization }}</option>
                  <option value="High">{{ 'High' | abpLocalization }}</option>
                  <option value="Urgent">{{ 'Urgent' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'IssueType' | abpLocalization }}</label>
                <select class="form-select" formControlName="issueType">
                  <option value="">-- Select --</option>
                  <option value="Bug">{{ 'Bug' | abpLocalization }}</option>
                  <option value="Feature Request">{{ 'FeatureRequest' | abpLocalization }}</option>
                  <option value="Complaint">{{ 'Complaint' | abpLocalization }}</option>
                  <option value="Question">{{ 'Question' | abpLocalization }}</option>
                  <option value="Other">{{ 'Other' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'RaisedVia' | abpLocalization }}</label>
                <select class="form-select" formControlName="raisedVia">
                  <option value="">-- Select --</option>
                  <option value="Email">{{ 'Email' | abpLocalization }}</option>
                  <option value="Phone">{{ 'Phone' | abpLocalization }}</option>
                  <option value="Website">{{ 'Website' | abpLocalization }}</option>
                  <option value="Walk-In">Walk-In</option>
                </select>
              </div>
            </div>
            <div class="mt-3">
              <label class="form-label">{{ 'Description' | abpLocalization }}</label>
              <textarea class="form-control" formControlName="description" rows="5" maxlength="4000"></textarea>
            </div>
            <div class="row g-3 mt-2">
              <div class="col-md-6">
                <label class="form-label">{{ '::Customer' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="customerId" placeholder="Customer ID (optional)">
              </div>
            </div>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/support/issues">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class IssueFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(IssueService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  form = this.fb.group({
    companyId: ['', Validators.required],
    subject: ['', [Validators.required, Validators.maxLength(500)]],
    description: [''],
    priority: ['Medium'],
    issueType: [''],
    raisedVia: [''],
    customerId: [''],
  });

  constructor() {
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });
  }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    this.service.create({
      companyId: val.companyId!,
      subject: val.subject!,
      description: val.description || undefined,
      priority: val.priority || undefined,
      issueType: val.issueType || undefined,
      raisedVia: val.raisedVia || undefined,
      customerId: val.customerId || undefined,
    }).subscribe({
      next: (created) => {
        this.toaster.success('Issue created');
        this.router.navigate(['/support/issues']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}