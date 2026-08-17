import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ContractTemplateService } from '../../proxy/crm/contract-template.service';

@Component({
  selector: 'app-contract-template-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(id ? '::Edit' : '::New') | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-8">
              <label class="form-label">{{ '::Title' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="title" maxlength="200">
            </div>
            <div class="col-md-4 d-flex align-items-end">
              <div class="form-check">
                <input type="checkbox" class="form-check-input" id="requiresFulfilment" formControlName="requiresFulfilment">
                <label class="form-check-label" for="requiresFulfilment">{{ '::RequiresFulfilment' | abpLocalization }}</label>
              </div>
            </div>
          </div>
          <div class="mt-3">
            <label class="form-label">{{ '::ContractTerms' | abpLocalization }} <small class="text-muted">({{ '::PlaceholderHint' | abpLocalization }})</small></label>
            <textarea class="form-control" formControlName="contractTerms" rows="6"></textarea>
          </div>
        </div></div>

        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h6 class="mb-0">{{ '::FulfilmentTerms' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addTerm()"><i class="fa fa-plus"></i></button>
          </div>
          <div formArrayName="fulfilmentTerms">
            @for (row of fulfilmentTerms.controls; track $index; let i = $index) {
              <div class="input-group input-group-sm mb-2" [formGroupName]="i">
                <input type="text" class="form-control" formControlName="termText">
                <button type="button" class="btn btn-outline-danger" (click)="removeTerm(i)"><i class="fa fa-trash"></i></button>
              </div>
            }
          </div>
        </div></div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/crm/contract-templates">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class ContractTemplateFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(ContractTemplateService);
  private toaster = inject(ToasterService);

  id: string | null = null;

  form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    contractTerms: [''],
    requiresFulfilment: [false],
    fulfilmentTerms: this.fb.array([]),
  });

  get fulfilmentTerms(): FormArray { return this.form.get('fulfilmentTerms') as FormArray; }

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.service.get(this.id).subscribe({
        next: (r) => {
          this.form.patchValue({ title: r.title, contractTerms: r.contractTerms ?? '', requiresFulfilment: r.requiresFulfilment });
          (r.fulfilmentTerms ?? []).forEach((t) => this.addTerm(t.termText));
        },
        error: () => {},
      });
    } else {
      this.id = null;
    }
  }

  addTerm(text = ''): void {
    this.fulfilmentTerms.push(this.fb.group({ termText: [text, Validators.required] }));
  }

  removeTerm(index: number): void { this.fulfilmentTerms.removeAt(index); }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const input = {
      title: val.title!,
      contractTerms: val.contractTerms || undefined,
      requiresFulfilment: val.requiresFulfilment ?? false,
      fulfilmentTerms: (val.fulfilmentTerms ?? []).map((t: any) => ({ termText: t.termText })),
    };
    const req$ = this.id ? this.service.update(this.id, input) : this.service.create(input);
    req$.subscribe({
      next: () => {
        this.toaster.success(this.id ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.router.navigate(['/crm/contract-templates']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
