import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { IndustryTypeService } from '../../proxy/crm/industry-type.service';

@Component({
  selector: 'app-industry-type-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(id ? '::Edit' : '::New') | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <label class="form-label">{{ 'Name' | abpLocalization }} *</label>
          <input type="text" class="form-control" formControlName="name" maxlength="200">
        </div></div>
        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/crm/industry-types">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class IndustryTypeFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(IndustryTypeService);
  private toaster = inject(ToasterService);

  id: string | null = null;

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.service.get(this.id).subscribe({
        next: (r) => this.form.patchValue({ name: r.name }),
        error: () => {},
      });
    } else {
      this.id = null;
    }
  }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const input = { name: val.name! };
    const req$ = this.id ? this.service.update(this.id, input) : this.service.create(input);
    req$.subscribe({
      next: () => {
        this.toaster.success(this.id ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.router.navigate(['/crm/industry-types']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
