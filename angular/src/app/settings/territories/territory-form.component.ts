import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { TerritoryService } from '../../proxy/core/territory.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { FiscalYearService } from '../../proxy/accounting/fiscal-year.service';
import { ItemGroupService } from '../../proxy/inventory/item-group.service';
import type { TerritoryDto, TerritoryTargetDto } from '../../proxy/core/models';
import type { EmployeeDto } from '../../proxy/human-resources/models';
import type { FiscalYearDto } from '../../proxy/accounting/models';
import type { ItemGroupDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-territory-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ (isEdit ? '::EditTerritory' : '::NewTerritory') | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">{{ '::TerritoryName' | abpLocalization }} *</label>
            <input type="text" class="form-control" formControlName="name" maxlength="140" [placeholder]="'::Placeholder:TerritoryExample' | abpLocalization">
          </div>

          <div class="mb-3">
            <label class="form-label">{{ '::ParentTerritory' | abpLocalization }}</label>
            <select class="form-select" formControlName="parentId">
              <option [ngValue]="null">{{ '::NoneRootTerritory' | abpLocalization }}</option>
              @for (g of territories(); track g.id) {
                @if (g.id !== id) {
                  <option [value]="g.id">{{ g.name }}</option>
                }
              }
            </select>
          </div>

          <div class="mb-3">
            <div class="form-check form-switch">
              <input type="checkbox" class="form-check-input" formControlName="isGroup" id="isGroup">
              <label class="form-check-label" for="isGroup">{{ '::IsGroupTerritoryHelp' | abpLocalization }}</label>
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">{{ '::TerritoryManager' | abpLocalization }}</label>
            <select class="form-select" formControlName="territoryManagerId">
              <option [ngValue]="null">{{ '::SelectTerritoryManager' | abpLocalization }}</option>
              @for (e of employees(); track e.id) {
                <option [value]="e.id">{{ e.employeeId ? e.employeeId + ' - ' : '' }}{{ e.fullName }}</option>
              }
            </select>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">{{ '::Save' | abpLocalization }}</button>
            <a routerLink="/settings/territories" class="btn btn-secondary">{{ '::Cancel' | abpLocalization }}</a>
          </div>
        </form>
      </div>
    </div>

    @if (isEdit && id) {
      <div class="card mt-3">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h6 class="card-title mb-0">{{ '::Targets' | abpLocalization }} ({{ targets().length }})</h6>
        </div>
        <div class="card-body">
          <div class="row g-2 align-items-end mb-3">
            <div class="col-md-3">
              <label class="form-label form-label-sm mb-1">{{ '::FiscalYear' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="newTarget.fiscalYearId">
                <option value="">{{ '::SelectFiscalYear' | abpLocalization }}</option>
                @for (fy of fiscalYears(); track fy.id) {
                  <option [value]="fy.id">{{ fy.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label form-label-sm mb-1">{{ '::ItemGroup' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="newTarget.itemGroupId">
                <option value="">{{ '::SelectItemGroup' | abpLocalization }}</option>
                @for (ig of itemGroups(); track ig.id) {
                  <option [value]="ig.id">{{ ig.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label form-label-sm mb-1">{{ '::TargetQty' | abpLocalization }}</label>
              <input type="number" class="form-control form-control-sm" [(ngModel)]="newTarget.targetQty" min="0" />
            </div>
            <div class="col-md-2">
              <label class="form-label form-label-sm mb-1">{{ '::TargetAmount' | abpLocalization }}</label>
              <input type="number" class="form-control form-control-sm" [(ngModel)]="newTarget.targetAmount" min="0" />
            </div>
            <div class="col-md-2">
              <button type="button" class="btn btn-sm btn-outline-primary w-100"
                      [disabled]="targetSaving() || (newTarget.targetQty <= 0 && newTarget.targetAmount <= 0)"
                      (click)="addTarget()">
                <i class="fa fa-plus me-1"></i>{{ '::AddTarget' | abpLocalization }}
              </button>
            </div>
          </div>

          @if (targets().length === 0) {
            <p class="text-muted small mb-0">{{ '::NoTargetsYet' | abpLocalization }}</p>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead>
                  <tr>
                    <th>{{ '::FiscalYear' | abpLocalization }}</th>
                    <th>{{ '::ItemGroup' | abpLocalization }}</th>
                    <th class="text-end">{{ '::TargetQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::TargetAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (t of targets(); track t.id) {
                    <tr>
                      <td>{{ t.fiscalYearName || '—' }}</td>
                      <td>{{ t.itemGroupName || '—' }}</td>
                      <td class="text-end">{{ t.targetQty | number:'1.0-2' }}</td>
                      <td class="text-end">{{ t.targetAmount | number:'1.2-2' }}</td>
                      <td class="text-end">
                        <button type="button" class="btn btn-sm btn-outline-danger" [disabled]="targetSaving()" (click)="removeTarget(t.id!)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    }
  `
})
export class TerritoryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(TerritoryService);
  private employeeService = inject(EmployeeService);
  private fiscalYearService = inject(FiscalYearService);
  private itemGroupService = inject(ItemGroupService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  territories = signal<TerritoryDto[]>([]);
  employees = signal<EmployeeDto[]>([]);
  fiscalYears = signal<FiscalYearDto[]>([]);
  itemGroups = signal<ItemGroupDto[]>([]);
  targets = signal<TerritoryTargetDto[]>([]);
  targetSaving = signal(false);
  newTarget = { fiscalYearId: '', itemGroupId: '', targetQty: 0, targetAmount: 0 };
  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(140)]],
      parentId: [null],
      isGroup: [false],
      territoryManagerId: [null],
    });
  }

  ngOnInit() {
    this.service.getList({ isGroup: true, maxResultCount: 200 } as any).subscribe(r => {
      this.territories.set(r.items ?? []);
    });

    this.employeeService.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe(r => {
      this.employees.set(r.items ?? []);
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.fiscalYearService.getList({ skipCount: 0, maxResultCount: 50 } as any).subscribe(r => {
        this.fiscalYears.set(r.items ?? []);
      });
      this.itemGroupService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
        this.itemGroups.set(r.items ?? []);
      });
      this.loadTerritory(this.id);
    }
  }

  private loadTerritory(id: string): void {
    this.service.get(id).subscribe(res => {
      this.form.patchValue(res);
      this.targets.set(res.targets ?? []);
    });
  }

  addTarget(): void {
    if (!this.id) return;
    if (this.newTarget.targetQty <= 0 && this.newTarget.targetAmount <= 0) return;
    this.targetSaving.set(true);
    this.service.addTarget(this.id, {
      fiscalYearId: this.newTarget.fiscalYearId || undefined,
      itemGroupId: this.newTarget.itemGroupId || undefined,
      targetQty: this.newTarget.targetQty || 0,
      targetAmount: this.newTarget.targetAmount || 0,
    }).subscribe({
      next: () => {
        this.targetSaving.set(false);
        this.newTarget = { fiscalYearId: '', itemGroupId: '', targetQty: 0, targetAmount: 0 };
        this.toaster.success('::SuccessfullySaved');
        this.loadTerritory(this.id!);
      },
      error: (err: any) => {
        this.targetSaving.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Failed');
      },
    });
  }

  removeTarget(targetId: string): void {
    if (!this.id || !targetId) return;
    this.targetSaving.set(true);
    this.service.removeTarget(this.id, targetId).subscribe({
      next: () => {
        this.targetSaving.set(false);
        this.toaster.success('::SuccessfullyDeleted');
        this.loadTerritory(this.id!);
      },
      error: (err: any) => {
        this.targetSaving.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Failed');
      },
    });
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/settings/territories']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
