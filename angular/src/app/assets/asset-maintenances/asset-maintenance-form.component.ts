import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetMaintenanceService } from '../../proxy/assets/asset-maintenance.service';
import { AssetService } from '../../proxy/assets/asset.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { MaintenancePeriodicity, maintenancePeriodicityOptions } from '../../proxy/assets/maintenance-periodicity.enum';
import type { AssetDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-maintenance-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditAssetMaintenance' : 'NewAssetMaintenance') | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ (isEdit() ? 'EditAssetMaintenance' : 'NewAssetMaintenance') | abpLocalization }}</h5>
          <a class="btn btn-outline-secondary btn-sm" routerLink="/assets/maintenances">
            <i class="fa fa-arrow-left me-1"></i>{{ 'Back' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row mb-3">
              <div class="col-md-6">
                <label class="form-label required">{{ 'Asset' | abpLocalization }}</label>
                @if (isEdit()) {
                  <input type="text" class="form-control" [value]="assetDisplayName()" readonly />
                } @else {
                  <select class="form-select" formControlName="assetId">
                    <option [ngValue]="null">{{ 'SelectAsset' | abpLocalization }}</option>
                    @for (a of assets(); track a.id) {
                      <option [value]="a.id">{{ a.assetName || a.assetNumber }}</option>
                    }
                  </select>
                }
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'MaintenanceManager' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="maintenanceManagerName" placeholder="Manager Name" />
              </div>
            </div>

            <div class="row mb-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'MaintenanceTeam' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="maintenanceTeamName" placeholder="Team Name" />
              </div>
            </div>

            <div class="d-flex justify-content-between align-items-center mt-4 mb-2">
              <h6 class="mb-0 fw-bold">{{ 'MaintenanceTasks' | abpLocalization }}</h6>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="addTask()">
                <i class="fa fa-plus me-1"></i>{{ 'AddTask' | abpLocalization }}
              </button>
            </div>

            <div formArrayName="tasks">
              <table class="table table-bordered align-middle">
                <thead class="table-light">
                  <tr>
                    <th class="required" style="width: 25%">{{ 'Task' | abpLocalization }}</th>
                    <th style="width: 15%">{{ 'Periodicity' | abpLocalization }}</th>
                    <th class="required" style="width: 15%">{{ 'StartDate' | abpLocalization }}</th>
                    <th style="width: 15%">{{ 'EndDate' | abpLocalization }}</th>
                    <th style="width: 15%">{{ 'AssignTo' | abpLocalization }}</th>
                    <th style="width: 10%">{{ 'Certificate' | abpLocalization }}</th>
                    <th style="width: 5%"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (taskGroup of taskControls; track $index) {
                    <tr [formGroupName]="$index">
                      <td>
                        <input type="text" class="form-control form-control-sm" formControlName="maintenanceTask" placeholder="Task name" />
                      </td>
                      <td>
                        <select class="form-select form-select-sm" formControlName="periodicity">
                          @for (opt of periodicityOptions; track opt.value) {
                            <option [value]="opt.value">{{ opt.key }}</option>
                          }
                        </select>
                      </td>
                      <td>
                        <input type="date" class="form-control form-control-sm" formControlName="startDate" />
                      </td>
                      <td>
                        <input type="date" class="form-control form-control-sm" formControlName="endDate" />
                      </td>
                      <td>
                        <input type="text" class="form-control form-control-sm" formControlName="assignToName" placeholder="Assignee" />
                      </td>
                      <td class="text-center">
                        <input type="checkbox" class="form-check-input" formControlName="certificateRequired" />
                      </td>
                      <td class="text-center">
                        <button type="button" class="btn btn-sm btn-link text-danger p-0" (click)="removeTask($index)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </td>
                    </tr>
                  }
                  @if (taskControls.length === 0) {
                    <tr>
                      <td colspan="7" class="text-center text-muted py-3">
                        {{ 'NoTasksAddedYet' | abpLocalization }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <div class="d-flex justify-content-end gap-2 mt-4">
              <a class="btn btn-outline-secondary" routerLink="/assets/maintenances">{{ 'Cancel' | abpLocalization }}</a>
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
                @if (isSaving()) {
                  <i class="fa fa-spinner fa-spin me-1"></i>
                }
                {{ 'Save' | abpLocalization }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetMaintenanceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(AssetMaintenanceService);
  private assetService = inject(AssetService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  isEdit = signal(false);
  isSaving = signal(false);
  maintenanceId = signal<string | null>(null);
  assetDisplayName = signal('');
  assets = signal<AssetDto[]>([]);
  periodicityOptions = maintenancePeriodicityOptions;

  form: FormGroup = this.fb.group({
    companyId: [null, Validators.required],
    assetId: [null, Validators.required],
    maintenanceManagerName: [''],
    maintenanceTeamName: [''],
    tasks: this.fb.array([]),
  });

  get tasksArray(): FormArray {
    return this.form.get('tasks') as FormArray;
  }

  get taskControls(): FormGroup[] {
    return this.tasksArray.controls as FormGroup[];
  }

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    const companyId = this.companyContext.selectedCompanyId();
    this.form.patchValue({ companyId });

    this.assetService.getList({ maxResultCount: 100 }).subscribe((res) => {
      this.assets.set(res.items || []);
    });

    if (id && id !== 'new') {
      this.isEdit.set(true);
      this.maintenanceId.set(id);
      this.loadMaintenance(id);
    } else {
      this.addTask();
    }
  }

  loadMaintenance(id: string) {
    this.service.get(id).subscribe((res) => {
      this.form.patchValue({
        companyId: res.companyId,
        assetId: res.assetId,
        maintenanceManagerName: res.maintenanceManagerName,
        maintenanceTeamName: res.maintenanceTeamName,
      });
      this.assetDisplayName.set(res.assetName || res.assetId);

      this.tasksArray.clear();
      (res.tasks || []).forEach((t) => {
        this.tasksArray.push(
          this.fb.group({
            maintenanceTask: [t.maintenanceTask, Validators.required],
            periodicity: [t.periodicity, Validators.required],
            startDate: [t.startDate ? t.startDate.substring(0, 10) : '', Validators.required],
            endDate: [t.endDate ? t.endDate.substring(0, 10) : ''],
            assignToName: [t.assignToName || ''],
            certificateRequired: [t.certificateRequired || false],
          })
        );
      });
    });
  }

  addTask() {
    const today = new Date().toISOString().substring(0, 10);
    this.tasksArray.push(
      this.fb.group({
        maintenanceTask: ['', Validators.required],
        periodicity: [MaintenancePeriodicity.Monthly, Validators.required],
        startDate: [today, Validators.required],
        endDate: [''],
        assignToName: [''],
        certificateRequired: [false],
      })
    );
  }

  removeTask(index: number) {
    this.tasksArray.removeAt(index);
  }

  save() {
    if (this.form.invalid) return;
    this.isSaving.set(true);

    const val = this.form.value;
    const tasks = (val.tasks || []).map((t: any) => ({
      maintenanceTask: t.maintenanceTask,
      periodicity: Number(t.periodicity),
      startDate: new Date(t.startDate).toISOString(),
      endDate: t.endDate ? new Date(t.endDate).toISOString() : null,
      assignToName: t.assignToName || null,
      certificateRequired: !!t.certificateRequired,
    }));

    if (this.isEdit()) {
      this.service
        .update(this.maintenanceId()!, {
          tasks,
        })
        .subscribe({
          next: () => {
            this.toaster.success('::SuccessfullyUpdated');
            this.router.navigate(['/assets/maintenances']);
          },
          error: (err) => {
            this.isSaving.set(false);
            this.toaster.error(err?.error?.error?.message ?? 'Save failed');
          },
        });
    } else {
      this.service
        .create({
          companyId: val.companyId,
          assetId: val.assetId,
          tasks,
        })
        .subscribe({
          next: () => {
            this.toaster.success('::SuccessfullyCreated');
            this.router.navigate(['/assets/maintenances']);
          },
          error: (err) => {
            this.isSaving.set(false);
            this.toaster.error(err?.error?.error?.message ?? 'Create failed');
          },
        });
    }
  }
}
