import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { WorkstationTypeService } from '../../proxy/manufacturing/workstation-type.service';
import type { WorkstationTypeDto } from '../../proxy/manufacturing/models';

@Component({
  selector: 'app-workstation-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? 'EditWorkstation' : 'NewWorkstation') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Name' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.name" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Type' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="onWorkstationTypeChange($event)" [(ngModel)]="form.workstationTypeId">
              <option [ngValue]="null">{{ '::None' | abpLocalization }}</option>
              @for (t of workstationTypes; track t.id) {
                <option [ngValue]="t.id">{{ t.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Capacity' | abpLocalization }}</label>
            <input type="number" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.productionCapacity" min="1" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'CostComponents' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Component' | abpLocalization }}</th><th>Cost/Hour</th><th></th></tr></thead>
          <tbody>
            @for (c of form.costs; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="c.component" [placeholder]="'::Placeholder:CostComponent' | abpLocalization" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="c.operatingCost" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.costs.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.costs.push({component:'',operatingCost:0})"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-between">
          <span class="fw-bold">{{ 'HourRate' | abpLocalization }}: {{ getHourRate() | number:'1.2-2' }}</span>
          <div class="d-flex gap-2">
            <a class="btn btn-secondary" routerLink="/manufacturing/workstations">{{ 'Cancel' | abpLocalization }}</a>
            <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class WorkstationFormComponent implements OnInit {
  private manufacturingService = inject(ManufacturingService);
  private workstationTypeService = inject(WorkstationTypeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  saving = false;
  isDirty = false;
  isEdit = false;
  workstationId: string | null = null;
  workstationTypes: WorkstationTypeDto[] = [];
  form: any = { name: '', workstationType: '', workstationTypeId: null, productionCapacity: 1, costs: [{ component: 'Labor', operatingCost: 0 }] };

  ngOnInit(): void {
    this.workstationTypeService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe((res) => {
      this.workstationTypes = res.items ?? [];
    });

    this.workstationId = this.route.snapshot.paramMap.get('id');
    if (this.workstationId) {
      this.isEdit = true;
      this.manufacturingService.getWorkstation(this.workstationId).subscribe((ws) => {
        this.form = {
          name: ws.name,
          workstationType: ws.workstationType ?? '',
          workstationTypeId: ws.workstationTypeId ?? null,
          productionCapacity: ws.productionCapacity ?? 1,
          description: ws.description ?? '',
          costs: (ws.costs ?? []).map(c => ({ component: c.name, operatingCost: c.amount })),
        };
      });
    }
  }

  /** Mirrors ERPNext workstation.py: picking a type copies its cost breakdown down
   * onto this Workstation. The server re-applies this authoritatively on save regardless;
   * this is just so the preview grid reflects it immediately. */
  onWorkstationTypeChange(typeId: string | null): void {
    this.isDirty = true;
    this.form.workstationTypeId = typeId;
    const type = this.workstationTypes.find(t => t.id === typeId);
    if (type) {
      this.form.workstationType = type.name;
      this.form.costs = (type.costs ?? []).map(c => ({ component: c.component, operatingCost: c.operatingCost }));
    }
  }

  getHourRate(): number { return this.form.costs.reduce((s: number, c: any) => s + (c.operatingCost || 0), 0); }

  save() {
    this.saving = true;
    const request = this.isEdit && this.workstationId
      ? this.manufacturingService.updateWorkstation(this.workstationId, this.form)
      : this.manufacturingService.createWorkstation(this.form);
    request.subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/manufacturing/workstations']); },
      error: () => { this.saving = false; },
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}
