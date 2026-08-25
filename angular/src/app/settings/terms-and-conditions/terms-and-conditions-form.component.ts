import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { TermsAndConditionsService } from '../../proxy/core/terms-and-conditions.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-terms-and-conditions-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Terms & Conditions</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Company *</label>
              <select class="form-select" formControlName="companyId">
                <option value="">— Select Company —</option>
                @for (c of companies(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-6">
              <label class="form-label">Title *</label>
              <input type="text" class="form-control" formControlName="title" maxlength="140" placeholder="e.g. Standard Domestic Sales Terms">
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Terms Content</label>
            <textarea class="form-control font-monospace" formControlName="terms" rows="8" placeholder="Enter standard legal / commercial clauses..."></textarea>
            <small class="form-text text-muted">Legal terms, warranty conditions, payment timelines, and dispute clauses.</small>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <div class="form-check form-switch">
                <input type="checkbox" class="form-check-input" formControlName="isSelling" id="isSelling">
                <label class="form-check-label" for="isSelling">Applicable for Selling</label>
              </div>
            </div>
            <div class="col-md-4">
              <div class="form-check form-switch">
                <input type="checkbox" class="form-check-input" formControlName="isBuying" id="isBuying">
                <label class="form-check-label" for="isBuying">Applicable for Buying</label>
              </div>
            </div>
            <div class="col-md-4">
              <div class="form-check form-switch">
                <input type="checkbox" class="form-check-input" formControlName="isDisabled" id="isDisabled">
                <label class="form-check-label" for="isDisabled">Disabled</label>
              </div>
            </div>
          </div>

          <div class="mb-3">
            <div class="form-check">
              <input type="checkbox" class="form-check-input" formControlName="copyAttachmentsToTransaction" id="copyAttachments">
              <label class="form-check-label" for="copyAttachments">Copy Attachments to Transaction</label>
            </div>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/settings/terms-and-conditions" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class TermsAndConditionsFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(TermsAndConditionsService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      title: ['', [Validators.required, Validators.maxLength(140)]],
      terms: [''],
      isSelling: [true],
      isBuying: [true],
      isDisabled: [false],
      copyAttachmentsToTransaction: [false],
    });
  }

  ngOnInit() {
    this.companyService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      this.companies.set(r.items ?? []);
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue(res);
      });
    } else {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/settings/terms-and-conditions']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
