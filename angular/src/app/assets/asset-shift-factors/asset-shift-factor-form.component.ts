import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetShiftFactorService } from '../../proxy/assets/asset-shift-factor.service';

@Component({
  selector: 'app-asset-shift-factor-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditAssetShiftFactor' : 'NewAssetShiftFactor') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'ShiftName' | abpLocalization }} *</label>
                <input class="form-control" formControlName="shiftName" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Factor' | abpLocalization }} *</label>
                <input type="number" class="form-control" formControlName="factor" step="0.01" min="0.01" />
              </div>
              <div class="col-md-3">
                <div class="form-check mt-4">
                  <input class="form-check-input" type="checkbox" formControlName="isDefault" id="isDefault" />
                  <label class="form-check-label" for="isDefault">{{ 'Default' | abpLocalization }}</label>
                </div>
              </div>
            </div>
            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" routerLink="/assets/shift-factors">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetShiftFactorFormComponent implements OnInit {
  private service = inject(AssetShiftFactorService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  isEdit = signal(false);
  isSaving = signal(false);
  editId = signal<string | null>(null);

  form = this.fb.group({
    shiftName: ['', Validators.required],
    factor: [1, [Validators.required, Validators.min(0.01)]],
    isDefault: [false],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.editId.set(id);
      this.service.get(id).subscribe(f => {
        this.form.patchValue({
          shiftName: f.shiftName,
          factor: f.factor,
          isDefault: f.isDefault,
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.isSaving.set(true);
    const val = this.form.getRawValue() as any;
    const req$ = this.isEdit()
      ? this.service.update(this.editId()!, val)
      : this.service.create(val);
    req$.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/shift-factors']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
