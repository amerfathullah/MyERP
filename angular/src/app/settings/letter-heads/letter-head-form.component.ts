import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { LetterHeadService } from '../../proxy/core/letter-head.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-letter-head-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Letter Head</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">Company *</label>
            <select class="form-select" formControlName="companyId">
              <option value="">— Select Company —</option>
              @for (c of companies(); track c.id) {
                <option [value]="c.id">{{ c.name }}</option>
              }
            </select>
          </div>
          <div class="mb-3">
            <label class="form-label">Name *</label>
            <input type="text" class="form-control" formControlName="letterHeadName" maxlength="140">
          </div>
          <div class="mb-3">
            <label class="form-label">For</label>
            <select class="form-select" formControlName="letterHeadFor">
              <option [ngValue]="0">DocType (transactions)</option>
              <option [ngValue]="1">Report</option>
            </select>
          </div>
          <div class="mb-3">
            <label class="form-label">Header Content</label>
            <textarea class="form-control" formControlName="headerContent" rows="5"></textarea>
            <small class="form-text text-muted">HTML or image URL for the document header.</small>
          </div>
          <div class="mb-3">
            <label class="form-label">Footer Content</label>
            <textarea class="form-control" formControlName="footerContent" rows="5"></textarea>
          </div>
          <div class="mb-3 form-check">
            <input type="checkbox" class="form-check-input" formControlName="isDefault" id="isDefault">
            <label class="form-check-label" for="isDefault">Default for this category</label>
          </div>
          <div class="mb-3 form-check">
            <input type="checkbox" class="form-check-input" formControlName="isDisabled" id="isDisabled">
            <label class="form-check-label" for="isDisabled">Disabled</label>
          </div>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
          <a routerLink="/settings/letter-heads" class="btn btn-secondary ms-2">Cancel</a>
        </form>
      </div>
    </div>
  `
})
export class LetterHeadFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(LetterHeadService);
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
      letterHeadName: ['', [Validators.required, Validators.maxLength(140)]],
      letterHeadFor: [0],
      headerContent: [''],
      footerContent: [''],
      isDefault: [false],
      isDisabled: [false],
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
        this.router.navigate(['/settings/letter-heads']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
