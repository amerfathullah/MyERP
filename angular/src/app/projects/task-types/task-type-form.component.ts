import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { TaskTypeService } from '../../proxy/projects/task-type.service';

@Component({
  selector: 'app-task-type-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ (isEdit ? '::EditTaskType' : '::NewTaskType') | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-8">
              <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="name" [placeholder]="'::Placeholder:TaskTypeExample' | abpLocalization">
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ '::Weight' | abpLocalization }} *</label>
              <input type="number" step="0.1" class="form-control" formControlName="weight" placeholder="1.0">
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">{{ '::Description' | abpLocalization }}</label>
            <textarea class="form-control" rows="3" formControlName="description" [placeholder]="'::Placeholder:OptionalDescription' | abpLocalization"></textarea>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">{{ '::Save' | abpLocalization }}</button>
            <a routerLink="/projects/task-types" class="btn btn-secondary">{{ '::Cancel' | abpLocalization }}</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class TaskTypeFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(TaskTypeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(140)]],
      weight: [1, [Validators.required, Validators.min(0)]],
      description: ['', Validators.maxLength(500)],
    });
  }

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue(res);
      });
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
        this.router.navigate(['/projects/task-types']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::Failed'),
    });
  }
}
