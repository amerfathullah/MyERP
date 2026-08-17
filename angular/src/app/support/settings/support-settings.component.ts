import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SupportSettingsService } from '../../proxy/support/support-settings.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-support-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::SupportSettings' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card"><div class="card-body">
          <div class="form-check mb-3">
            <input type="checkbox" class="form-check-input" id="track" formControlName="trackServiceLevelAgreement">
            <label class="form-check-label" for="track">{{ '::TrackServiceLevelAgreement' | abpLocalization }}</label>
          </div>
          <div class="form-check mb-3">
            <input type="checkbox" class="form-check-input" id="allowReset" formControlName="allowResettingServiceLevelAgreement">
            <label class="form-check-label" for="allowReset">{{ '::AllowResettingServiceLevelAgreement' | abpLocalization }}</label>
          </div>
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ '::CloseIssueAfterDays' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="closeIssueAfterDays" min="0">
            </div>
          </div>
        </div></div>
        <div class="d-flex justify-content-end gap-2 mt-3">
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class SupportSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(SupportSettingsService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  form = this.fb.group({
    trackServiceLevelAgreement: [true],
    allowResettingServiceLevelAgreement: [false],
    closeIssueAfterDays: [null as number | null],
  });

  ngOnInit(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.service.getForCompany(companyId).subscribe({
      next: (r) => {
        if (r) {
          this.form.patchValue({
            trackServiceLevelAgreement: r.trackServiceLevelAgreement,
            allowResettingServiceLevelAgreement: r.allowResettingServiceLevelAgreement,
            closeIssueAfterDays: r.closeIssueAfterDays ?? null,
          });
        }
      },
      error: () => {},
    });
  }

  save(): void {
    if (this.form.invalid) return;
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    const val = this.form.getRawValue();
    this.service.save({
      companyId,
      trackServiceLevelAgreement: val.trackServiceLevelAgreement ?? true,
      allowResettingServiceLevelAgreement: val.allowResettingServiceLevelAgreement ?? false,
      closeIssueAfterDays: val.closeIssueAfterDays,
    }).subscribe({
      next: () => this.toaster.success('::SuccessfullyUpdated'),
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
