import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetCategoryService } from '../../proxy/assets/asset-category.service';

@Component({
  selector: 'app-asset-category-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditAssetCategory' : 'NewAssetCategory') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'CategoryName' | abpLocalization }} *</label>
                <input class="form-control" formControlName="categoryName" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Depreciable' | abpLocalization }}</label>
                <div class="form-check mt-2">
                  <input class="form-check-input" type="checkbox" formControlName="isDepreciable" id="isDepreciable" />
                  <label class="form-check-label" for="isDepreciable">{{ 'EnableDepreciation' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'DepreciationMethod' | abpLocalization }}</label>
                <select class="form-select" formControlName="defaultDepreciationMethod">
                  <option [value]="0">Straight Line</option>
                  <option [value]="1">Double Declining Balance</option>
                  <option [value]="2">Written Down Value</option>
                  <option [value]="3">Manual</option>
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'UsefulLifeMonths' | abpLocalization }}</label>
                <input type="number" class="form-control" formControlName="defaultUsefulLifeMonths" min="1" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'DepreciationRate' | abpLocalization }} (%)</label>
                <input type="number" class="form-control" formControlName="defaultDepreciationRate" step="0.01" />
              </div>
            </div>
            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" routerLink="/assets/categories">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetCategoryFormComponent implements OnInit {
  private service = inject(AssetCategoryService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  isEdit = signal(false);
  isSaving = signal(false);
  editId = signal<string | null>(null);

  form = this.fb.group({
    categoryName: ['', Validators.required],
    isDepreciable: [true],
    defaultDepreciationMethod: [0],
    defaultUsefulLifeMonths: [60],
    defaultDepreciationRate: [null as number | null],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.editId.set(id);
      this.service.get(id).subscribe(cat => {
        this.form.patchValue({
          categoryName: cat.categoryName,
          isDepreciable: cat.isDepreciable,
          defaultDepreciationMethod: cat.defaultDepreciationMethod as any,
          defaultUsefulLifeMonths: cat.defaultUsefulLifeMonths,
          defaultDepreciationRate: cat.defaultDepreciationRate ?? null,
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
        this.router.navigate(['/assets/categories']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
