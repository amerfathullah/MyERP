import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { OpportunityLostReasonService } from '../../proxy/crm/opportunity-lost-reason.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-opportunity-lost-reason-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Opportunity Lost Reason</h5>
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
            <label class="form-label">Reason *</label>
            <input type="text" class="form-control" formControlName="reason" maxlength="140" placeholder="e.g. Price Too High, Competitor Won, Project Cancelled">
          </div>

          <div class="mb-3">
            <label class="form-label">Description</label>
            <textarea class="form-control" formControlName="description" rows="3" maxlength="500" placeholder="Additional details or guidelines for using this reason..."></textarea>
          </div>

          <div class="mb-3">
            <div class="form-check form-switch">
              <input type="checkbox" class="form-check-input" formControlName="isDisabled" id="isDisabled">
              <label class="form-check-label" for="isDisabled">Disabled</label>
            </div>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/crm/opportunity-lost-reasons" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class OpportunityLostReasonFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(OpportunityLostReasonService);
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
      reason: ['', [Validators.required, Validators.maxLength(140)]],
      description: ['', Validators.maxLength(500)],
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
        this.router.navigate(['/crm/opportunity-lost-reasons']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
