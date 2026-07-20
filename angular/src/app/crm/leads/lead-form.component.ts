import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LeadStore } from '../store/lead.store';
import { LeadService } from '../../proxy/crm/lead.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-lead-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './lead-form.component.html',
  styleUrls: ['./lead-form.component.scss'],
})
export class LeadFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(LeadStore);
  private service = inject(LeadService);
  private companyContext = inject(CompanyContextService);

  form!: FormGroup;
  isEditMode = false;
  entityId: string | null = null;

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.form = this.fb.group({
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.maxLength(100)]],
      companyName: ['', [Validators.maxLength(200)]],
      email: ['', [Validators.email, Validators.maxLength(256)]],
      phone: ['', [Validators.maxLength(30)]],
      mobileNo: ['', [Validators.maxLength(30)]],
      jobTitle: ['', [Validators.maxLength(100)]],
      website: ['', [Validators.maxLength(500)]],
      source: [0],
      city: [''],
      state: [''],
      country: ['Malaysia'],
      industry: [''],
      annualRevenue: [null],
      companyId: ['', Validators.required],
      notes: [''],
    });

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((lead) => {
        this.form.patchValue(lead);
      });
    } else {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();

    if (this.isEditMode) {
      this.service.update(this.entityId!, value as any).subscribe({
        next: () => this.router.navigate(['/crm/leads']),
        error: () => {},
      });
    } else {
      this.service.create(value as any).subscribe({
        next: () => this.router.navigate(['/crm/leads']),
        error: () => {},
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}