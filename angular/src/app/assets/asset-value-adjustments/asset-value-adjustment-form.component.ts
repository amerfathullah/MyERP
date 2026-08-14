import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetValueAdjustmentService } from '../../proxy/assets/asset-value-adjustment.service';
import { AssetService } from '../../proxy/assets/asset.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AssetDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-value-adjustment-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewAssetValueAdjustment' | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'Asset' | abpLocalization }} *</label>
                <select class="form-select" formControlName="assetId" (change)="onAssetChange()">
                  <option value="">— Select Asset —</option>
                  @for (a of assets(); track a.id) {
                    <option [value]="a.id">{{ a.assetName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Date' | abpLocalization }} *</label>
                <input type="date" class="form-control" formControlName="date" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'CurrentValue' | abpLocalization }}</label>
                <input type="number" class="form-control" formControlName="currentAssetValue" [readonly]="true" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'NewValue' | abpLocalization }} *</label>
                <input type="number" class="form-control" formControlName="newAssetValue" step="0.01" (input)="calcDiff()" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Difference' | abpLocalization }}</label>
                <input type="number" class="form-control" [value]="difference()" [readonly]="true"
                  [class.text-success]="difference() > 0" [class.text-danger]="difference() < 0" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'Notes' | abpLocalization }}</label>
                <textarea class="form-control" formControlName="notes" rows="2"></textarea>
              </div>
            </div>
            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" routerLink="/assets/value-adjustments">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetValueAdjustmentFormComponent implements OnInit {
  private service = inject(AssetValueAdjustmentService);
  private assetService = inject(AssetService);
  private companyContext = inject(CompanyContextService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  assets = signal<AssetDto[]>([]);
  difference = signal(0);
  isSaving = signal(false);

  form = this.fb.group({
    assetId: ['', Validators.required],
    date: [new Date().toISOString().split('T')[0], Validators.required],
    currentAssetValue: [0],
    newAssetValue: [0, [Validators.required, Validators.min(0)]],
    notes: [''],
  });

  ngOnInit(): void {
    this.assetService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      this.assets.set(r.items ?? []);
    });
  }

  onAssetChange(): void {
    const assetId = this.form.get('assetId')?.value;
    const asset = this.assets().find(a => a.id === assetId);
    if (asset) {
      this.form.get('currentAssetValue')?.setValue(asset.valueAfterDepreciation ?? asset.purchaseAmount ?? 0);
      this.calcDiff();
    }
  }

  calcDiff(): void {
    const cur = this.form.get('currentAssetValue')?.value ?? 0;
    const nw = this.form.get('newAssetValue')?.value ?? 0;
    this.difference.set(Number(nw) - Number(cur));
  }

  save(): void {
    if (this.form.invalid) return;
    this.isSaving.set(true);
    const val = this.form.getRawValue();
    const cid = this.companyContext.currentCompanyId();
    this.service.create({ ...val, companyId: cid! } as any).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/value-adjustments']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
