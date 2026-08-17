import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { IssuePriorityService } from '../../proxy/support/issue-priority.service';

@Component({
  selector: 'app-issue-priority-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(id ? '::Edit' : '::New') | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3"><div class="card-body">
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'Name' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="name" maxlength="50">
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'Description' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="description" maxlength="500">
            </div>
          </div>
        </div></div>
        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/support/issue-priorities">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class IssuePriorityFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(IssuePriorityService);
  private toaster = inject(ToasterService);

  id: string | null = null;

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(50)]],
    description: [''],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.service.get(this.id).subscribe({
        next: (r) => this.form.patchValue({ name: r.name, description: r.description ?? '' }),
        error: () => {},
      });
    } else {
      this.id = null;
    }
  }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const input = { name: val.name!, description: val.description || undefined };
    const req$ = this.id ? this.service.update(this.id, input) : this.service.create(input);
    req$.subscribe({
      next: () => {
        this.toaster.success(this.id ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.router.navigate(['/support/issue-priorities']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
