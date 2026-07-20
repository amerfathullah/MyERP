import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SalaryStructureService } from '../../proxy/human-resources';

@Component({
  selector: 'app-salary-structure-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewSalaryStructure' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-5">
            <label class="form-label">{{ 'Name' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.name" placeholder="e.g., Executive Grade A" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'PayrollFrequency' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.payrollFrequency">
              <option>{{ 'Monthly' | abpLocalization }}</option>
              <option>{{ 'Bimonthly' | abpLocalization }}</option>
              <option>{{ 'Weekly' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <div class="form-check mt-4">
              <input type="checkbox" class="form-check-input" (ngModelChange)="isDirty=true" [(ngModel)]="form.isHourlyBased" id="hourly" />
              <label class="form-check-label" for="hourly">{{ 'HourlyBased' | abpLocalization }}</label>
            </div>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Components' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Component' | abpLocalization }}</th><th>{{ 'Amount' | abpLocalization }}</th><th>{{ 'Formula' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (d of form.details; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="d.componentName" placeholder="e.g., Basic, HRA" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="d.amount" /></td>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="d.formula" placeholder="e.g., B * 0.4" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.details.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.details.push({componentName:'',amount:0,formula:''})">
          <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
        </button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/hr/salary-structures">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class SalaryStructureFormComponent {
  private salaryStructureService = inject(SalaryStructureService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = { name: '', payrollFrequency: 'Monthly', isHourlyBased: false, details: [{ componentName: 'Basic', amount: 0, formula: '' }] };

  save() {
    this.saving = true;
    this.salaryStructureService.create(this.form)
      .subscribe({ next: () => this.router.navigate(['/hr/salary-structures']), error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}