import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesStageService } from '../../proxy/crm/sales-stage.service';

@Component({
  selector: 'app-sales-stage-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(id ? '::Edit' : '::New') | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body row g-3">
          <div class="col-md-8">
            <label class="form-label">{{ 'StageName' | abpLocalization }} *</label>
            <input type="text" class="form-control" formControlName="stageName" maxlength="100">
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'SortOrder' | abpLocalization }}</label>
            <input type="number" class="form-control" formControlName="sortOrder">
          </div>
        </div></div>
        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/crm/sales-stages">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class SalesStageFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(SalesStageService);
  private toaster = inject(ToasterService);

  id: string | null = null;

  form = this.fb.group({
    stageName: ['', [Validators.required, Validators.maxLength(100)]],
    sortOrder: [0],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.service.get(this.id).subscribe({
        next: (r) => this.form.patchValue({ stageName: r.stageName, sortOrder: r.sortOrder ?? 0 }),
        error: () => {},
      });
    } else {
      this.id = null;
    }
  }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const input = { stageName: val.stageName!, sortOrder: val.sortOrder ?? 0 };
    const req$ = this.id ? this.service.update(this.id, input) : this.service.create(input);
    req$.subscribe({
      next: () => {
        this.toaster.success(this.id ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.router.navigate(['/crm/sales-stages']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
