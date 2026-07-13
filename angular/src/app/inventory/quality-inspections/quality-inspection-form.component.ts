import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-qi-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewInspection' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Item' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.itemName" placeholder="Item name" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'InspectionType' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.inspectionType">
              <option [ngValue]="0">Incoming</option>
              <option [ngValue]="1">Outgoing</option>
              <option [ngValue]="2">In Process</option>
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'InspectionDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.inspectionDate" />
          </div>
        </div>
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'SampleSize' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.sampleSize" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'BatchNo' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.batchNo" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'Readings' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Specification' | abpLocalization }}</th><th>Min</th><th>Max</th><th>{{ 'Reading' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (r of form.readings; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="r.specification" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="r.minValue" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="r.maxValue" /></td>
                <td><input class="form-control form-control-sm" [(ngModel)]="r.readingValue" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.readings.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addReading()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/inventory/quality-inspections">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class QualityInspectionFormComponent {
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  form: any = { inspectionDate: new Date().toISOString().split('T')[0], inspectionType: 0, sampleSize: 1, itemName: '', readings: [{ specification: '', minValue: null, maxValue: null, readingValue: '', isNumeric: true }] };

  addReading() { this.form.readings.push({ specification: '', minValue: null, maxValue: null, readingValue: '', isNumeric: true }); }

  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/quality-inspection', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => this.router.navigate(['/inventory/quality-inspections']), error: () => { this.saving = false; } });
  }
}
