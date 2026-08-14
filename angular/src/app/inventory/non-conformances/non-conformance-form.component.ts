import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import { NonConformanceStatus } from '../../proxy/inventory/non-conformance-status.enum';

@Component({
  selector: 'app-non-conformance-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? '::EditNonConformance' : 'NewNonConformance') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3 mb-3">
              <div class="col-md-8">
                <label class="form-label">{{ '::Subject' | abpLocalization }} *</label>
                <input type="text" class="form-control form-control-sm" formControlName="subject" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::ProcessOwner' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm" formControlName="processOwner" />
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::Details' | abpLocalization }}</label>
                <textarea class="form-control form-control-sm" rows="3" formControlName="details"></textarea>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::CorrectiveAction' | abpLocalization }}</label>
                <textarea class="form-control form-control-sm" rows="3" formControlName="correctiveAction"></textarea>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::PreventiveAction' | abpLocalization }}</label>
                <textarea class="form-control form-control-sm" rows="3" formControlName="preventiveAction"></textarea>
              </div>
            </div>

            <div class="d-flex justify-content-between align-items-center">
              <div class="d-flex gap-2">
                @if (isEdit && id && status === NonConformanceStatus.Open) {
                  <button type="button" class="btn btn-success btn-sm" (click)="resolve()">
                    <i class="fa fa-check me-1"></i>Resolve
                  </button>
                  <button type="button" class="btn btn-danger btn-sm" (click)="cancel()">
                    <i class="fa fa-ban me-1"></i>Cancel
                  </button>
                }
              </div>
              <div class="d-flex gap-2">
                <a routerLink="/inventory/non-conformances" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
                <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || isSaving">
                  <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
                </button>
              </div>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class NonConformanceFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(QualityManagementService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly NonConformanceStatus = NonConformanceStatus;

  form!: FormGroup;
  isEdit = false;
  isSaving = false;
  id: string | null = null;
  status: NonConformanceStatus = NonConformanceStatus.Open;

  ngOnInit() {
    this.form = this.fb.group({
      companyId: ['00000000-0000-0000-0000-000000000000'],
      subject: ['', Validators.required],
      processOwner: [''],
      details: [''],
      correctiveAction: [''],
      preventiveAction: [''],
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.isEdit = true;
      this.service.getNonConformance(this.id).subscribe((res) => {
        this.status = res.status ?? NonConformanceStatus.Open;
        this.form.patchValue({
          companyId: res.companyId ?? '00000000-0000-0000-0000-000000000000',
          subject: res.subject ?? '',
          processOwner: res.processOwner ?? '',
          details: res.details ?? '',
          correctiveAction: res.correctiveAction ?? '',
          preventiveAction: res.preventiveAction ?? '',
        });
      });
    }
  }

  save() {
    if (this.form.invalid) return;

    this.isSaving = true;
    const val = this.form.value;

    const op = this.isEdit && this.id
      ? this.service.updateNonConformance(this.id, val)
      : this.service.createNonConformance(val);

    op.subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/inventory/non-conformances']);
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      },
    });
  }

  resolve() {
    if (!this.id) return;
    this.service.resolveNonConformance(this.id, {
      correctiveAction: this.form.get('correctiveAction')?.value,
      preventiveAction: this.form.get('preventiveAction')?.value,
    }).subscribe(() => {
      this.status = NonConformanceStatus.Resolved;
      this.toaster.success('Non-conformance resolved.');
    });
  }

  cancel() {
    if (!this.id) return;
    this.service.cancelNonConformance(this.id).subscribe(() => {
      this.status = NonConformanceStatus.Cancelled;
      this.toaster.success('Non-conformance cancelled.');
    });
  }
}
