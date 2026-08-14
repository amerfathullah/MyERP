import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetMovementService } from '../../proxy/assets/asset-movement.service';
import { AssetService } from '../../proxy/assets/asset.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AssetDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-movement-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewAssetMovement' | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'Asset' | abpLocalization }} *</label>
                <select class="form-select" formControlName="assetId">
                  <option value="">— Select Asset —</option>
                  @for (a of assets(); track a.id) {
                    <option [value]="a.id">{{ a.assetName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Purpose' | abpLocalization }} *</label>
                <select class="form-select" formControlName="movementType">
                  <option value="Transfer">Transfer</option>
                  <option value="Issue">Issue</option>
                  <option value="Receipt">Receipt</option>
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Date' | abpLocalization }} *</label>
                <input type="date" class="form-control" formControlName="movementDate" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'FromLocation' | abpLocalization }}</label>
                <input class="form-control" formControlName="sourceLocation" />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'ToLocation' | abpLocalization }}</label>
                <input class="form-control" formControlName="targetLocation" />
              </div>
            </div>
            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" routerLink="/assets/movements">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetMovementFormComponent implements OnInit {
  private service = inject(AssetMovementService);
  private assetService = inject(AssetService);
  private companyContext = inject(CompanyContextService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  assets = signal<AssetDto[]>([]);
  isSaving = signal(false);

  form = this.fb.group({
    assetId: ['', Validators.required],
    movementType: ['Transfer', Validators.required],
    movementDate: [new Date().toISOString().split('T')[0], Validators.required],
    sourceLocation: [''],
    targetLocation: [''],
    sourceEmployeeId: [null as string | null],
    targetEmployeeId: [null as string | null],
  });

  ngOnInit(): void {
    this.assetService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      this.assets.set(r.items ?? []);
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.isSaving.set(true);
    const val = this.form.getRawValue();
    const cid = this.companyContext.currentCompanyId();
    this.service.create({ ...val, companyId: cid ?? undefined, purpose: val.movementType } as any).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/movements']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
