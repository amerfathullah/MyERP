import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';

@Component({
  selector: 'app-workstation-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewWorkstation' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Name' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.name" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Type' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.workstationType" placeholder="e.g., CNC, Lathe" />
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
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="c.component" placeholder="e.g., Labor, Electricity" /></td>
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
export class WorkstationFormComponent {
  private manufacturingService = inject(ManufacturingService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = { name: '', workstationType: '', productionCapacity: 1, costs: [{ component: 'Labor', operatingCost: 0 }] };

  getHourRate(): number { return this.form.costs.reduce((s: number, c: any) => s + (c.operatingCost || 0), 0); }

  save() {
    this.saving = true;
    this.manufacturingService.createWorkstation(this.form)
      .subscribe({ next: () => this.router.navigate(['/manufacturing/workstations']), error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}