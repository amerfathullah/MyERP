import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetRepairService } from '../../proxy/assets/asset-repair.service';
import { AssetService } from '../../proxy/assets/asset.service';
import { AssetStatus } from '../../proxy/assets/asset-status.enum';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import { ProjectService } from '../../proxy/projects/project.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AssetDto } from '../../proxy/assets/models';
import type { CostCenterDto } from '../../proxy/accounting/models';
import type { ProjectDto } from '../../proxy/projects/models';

@Component({
  selector: 'app-asset-repair-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditAssetRepair' : 'NewRepair') | abpLocalization">
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
                @if (selectedAssetFullyDepreciated()) {
                  <small class="text-muted">{{ 'AssetFullyDepreciatedRepairHint' | abpLocalization }}</small>
                }
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'FailureDate' | abpLocalization }} *</label>
                <input type="date" class="form-control" formControlName="failureDate" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'CompletionDate' | abpLocalization }}</label>
                <input type="date" class="form-control" formControlName="completionDate" />
              </div>
              <div class="col-12">
                <label class="form-label">{{ 'Description' | abpLocalization }}</label>
                <textarea class="form-control" rows="2" formControlName="repairDescription"></textarea>
              </div>
              <div class="col-12">
                <label class="form-label">{{ 'ActionsPerformed' | abpLocalization }}</label>
                <textarea class="form-control" rows="2" formControlName="actionsPerformed"></textarea>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'Downtime' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="downtime" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'RepairCost' | abpLocalization }}</label>
                <input type="number" min="0" step="0.01" class="form-control" formControlName="repairCost" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'CostCenter' | abpLocalization }}</label>
                <select class="form-select" formControlName="costCenterId">
                  <option [ngValue]="null">—</option>
                  @for (cc of costCenters(); track cc.id) {
                    <option [ngValue]="cc.id">{{ cc.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'Project' | abpLocalization }}</label>
                <select class="form-select" formControlName="projectId">
                  <option [ngValue]="null">—</option>
                  @for (p of projects(); track p.id) {
                    <option [ngValue]="p.id">{{ p.projectName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-4 d-flex align-items-end">
                <div class="form-check">
                  <input type="checkbox" class="form-check-input" id="capitalize"
                    formControlName="capitalizeRepairCost" [attr.disabled]="selectedAssetFullyDepreciated() ? true : null">
                  <label class="form-check-label" for="capitalize">{{ 'Capitalize' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'IncreaseInAssetLifeMonths' | abpLocalization }}</label>
                <input type="number" min="0" step="1" class="form-control" formControlName="increaseInAssetLife"
                  [attr.disabled]="(!form.value.capitalizeRepairCost || selectedAssetFullyDepreciated()) ? true : null" />
              </div>
            </div>
            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" [routerLink]="cancelLink()">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetRepairFormComponent implements OnInit {
  private service = inject(AssetRepairService);
  private assetService = inject(AssetService);
  private costCenterService = inject(CostCenterService);
  private projectService = inject(ProjectService);
  private companyContext = inject(CompanyContextService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  assets = signal<AssetDto[]>([]);
  costCenters = signal<CostCenterDto[]>([]);
  projects = signal<ProjectDto[]>([]);
  isSaving = signal(false);
  repairId = signal<string | null>(null);
  isEdit = computed(() => !!this.repairId());
  cancelLink = computed(() => this.repairId() ? `/assets/repairs/${this.repairId()}` : '/assets/repairs');

  form = this.fb.group({
    assetId: ['', Validators.required],
    failureDate: [new Date().toISOString().split('T')[0], Validators.required],
    completionDate: [null as string | null],
    repairDescription: [''],
    actionsPerformed: [''],
    downtime: [''],
    repairCost: [0],
    costCenterId: [null as string | null],
    projectId: [null as string | null],
    capitalizeRepairCost: [false],
    increaseInAssetLife: [0],
  });

  selectedAssetFullyDepreciated = computed(() => {
    const id = this.form.get('assetId')?.value;
    const asset = this.assets().find(a => a.id === id);
    return !!asset && (asset.isFullyDepreciated || asset.status === AssetStatus.FullyDepreciated);
  });

  ngOnInit(): void {
    this.assetService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      this.assets.set((r.items ?? []).filter(a => a.status !== AssetStatus.Sold && a.status !== AssetStatus.Scrapped));
    });
    this.costCenterService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any).subscribe(r => {
      this.costCenters.set(r.items ?? []);
    });
    this.projectService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any).subscribe(r => {
      this.projects.set(r.items ?? []);
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.repairId.set(id);
      this.service.get(id).subscribe(repair => {
        this.form.patchValue({
          assetId: repair.assetId,
          failureDate: repair.failureDate ? repair.failureDate.split('T')[0] : null,
          completionDate: repair.completionDate ? repair.completionDate.split('T')[0] : null,
          repairDescription: repair.repairDescription ?? '',
          actionsPerformed: repair.actionsPerformed ?? '',
          downtime: repair.downtime ?? '',
          repairCost: repair.repairCost,
          costCenterId: repair.costCenterId ?? null,
          projectId: repair.projectId ?? null,
          capitalizeRepairCost: repair.capitalizeRepairCost,
          increaseInAssetLife: repair.increaseInAssetLife,
        } as any);
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.isSaving.set(true);
    const val = this.form.getRawValue();
    const cid = this.companyContext.currentCompanyId();
    const payload = { ...val, companyId: cid ?? undefined };

    const id = this.repairId();
    const request$ = id ? this.service.update(id, payload as any) : this.service.create(payload as any);
    request$.subscribe({
      next: (result) => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/repairs', result.id]);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
