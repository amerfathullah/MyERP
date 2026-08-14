import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import { QualityActionStatus } from '../../proxy/inventory/quality-action-status.enum';
import { QualityActionType } from '../../proxy/inventory/quality-action-type.enum';

@Component({
  selector: 'app-quality-action-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? '::EditQualityAction' : 'NewQualityAction') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3 mb-3">
              <div class="col-md-6">
                <label class="form-label">{{ '::Type' | abpLocalization }} *</label>
                <select class="form-select form-select-sm" formControlName="actionType">
                  <option [value]="QualityActionType.Corrective">Corrective</option>
                  <option [value]="QualityActionType.Preventive">Preventive</option>
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::Status' | abpLocalization }}</label>
                <div>
                  <span class="badge py-2 px-3" [ngClass]="getStatusBadgeClass(status)">
                    {{ getStatusLabel(status) }}
                  </span>
                </div>
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::ProblemDescription' | abpLocalization }} *</label>
                <textarea class="form-control form-control-sm" rows="3" formControlName="problemDescription"></textarea>
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::Resolution' | abpLocalization }}</label>
                <textarea class="form-control form-control-sm" rows="3" formControlName="resolution"></textarea>
              </div>
            </div>

            <div class="d-flex justify-content-between align-items-center">
              <div class="d-flex gap-2">
                @if (isEdit && id && status !== QualityActionStatus.Closed) {
                  <button type="button" class="btn btn-success btn-sm" (click)="resolve()">
                    <i class="fa fa-check me-1"></i>Resolve
                  </button>
                  <button type="button" class="btn btn-secondary btn-sm" (click)="close()">
                    <i class="fa fa-archive me-1"></i>Close
                  </button>
                }
              </div>
              <div class="d-flex gap-2">
                <a routerLink="/inventory/quality-actions" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
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
export class QualityActionFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(QualityManagementService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly QualityActionStatus = QualityActionStatus;
  readonly QualityActionType = QualityActionType;

  form!: FormGroup;
  isEdit = false;
  isSaving = false;
  id: string | null = null;
  status: QualityActionStatus = QualityActionStatus.Open;

  ngOnInit() {
    this.form = this.fb.group({
      companyId: ['00000000-0000-0000-0000-000000000000'],
      actionType: [QualityActionType.Corrective, Validators.required],
      problemDescription: ['', Validators.required],
      resolution: [''],
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.isEdit = true;
      this.service.getAction(this.id).subscribe((res) => {
        this.status = res.status ?? QualityActionStatus.Open;
        this.form.patchValue({
          companyId: res.companyId ?? '00000000-0000-0000-0000-000000000000',
          actionType: res.actionType ?? QualityActionType.Corrective,
          problemDescription: res.problemDescription ?? '',
          resolution: res.resolution ?? '',
        });
      });
    }
  }

  save() {
    if (this.form.invalid) return;

    this.isSaving = true;
    const val = this.form.value;

    const op = this.isEdit && this.id
      ? this.service.updateAction(this.id, val)
      : this.service.createAction(val);

    op.subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/inventory/quality-actions']);
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      },
    });
  }

  resolve() {
    if (!this.id) return;
    const res = this.form.get('resolution')?.value;
    if (!res) {
      this.toaster.warn('Please enter resolution text before marking resolved.');
      return;
    }
    this.service.resolveAction(this.id, { resolution: res }).subscribe(() => {
      this.status = QualityActionStatus.Resolved;
      this.toaster.success('Action marked as resolved.');
    });
  }

  close() {
    if (!this.id) return;
    this.service.closeAction(this.id).subscribe(() => {
      this.status = QualityActionStatus.Closed;
      this.toaster.success('Action closed.');
    });
  }

  getStatusBadgeClass(status: QualityActionStatus): string {
    switch (Number(status)) {
      case QualityActionStatus.Resolved:
        return 'bg-success';
      case QualityActionStatus.Closed:
        return 'bg-secondary';
      default:
        return 'bg-warning text-dark';
    }
  }

  getStatusLabel(status: QualityActionStatus): string {
    switch (Number(status)) {
      case QualityActionStatus.Resolved:
        return 'Resolved';
      case QualityActionStatus.Closed:
        return 'Closed';
      default:
        return 'Open';
    }
  }
}
