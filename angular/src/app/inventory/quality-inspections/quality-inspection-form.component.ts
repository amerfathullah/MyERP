import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityInspectionService } from '../../proxy/inventory';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-qi-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewInspection' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Item' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.itemId">
              <option value="">-- {{ 'SelectItem' | abpLocalization }} --</option>
              @for (i of availableItems(); track i.id) {
                <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'InspectionType' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.inspectionType">
              <option [ngValue]="0">{{ 'Incoming' | abpLocalization }}</option>
              <option [ngValue]="1">{{ 'Outgoing' | abpLocalization }}</option>
              <option [ngValue]="2">{{ 'InProcess' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'InspectionDate' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.inspectionDate" />
          </div>
        </div>
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'SampleSize' | abpLocalization }}</label>
            <input type="number" class="form-control" min="1" (ngModelChange)="isDirty=true" [(ngModel)]="form.sampleSize" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'BatchNo' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.batchNo" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'Readings' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Specification' | abpLocalization }}</th><th>{{ 'Min' | abpLocalization }}</th><th>{{ 'Max' | abpLocalization }}</th><th>{{ 'Reading' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (r of form.readings; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="r.specification" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="r.minValue" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="r.maxValue" /></td>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="r.readingValue" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.readings.splice($index,1); isDirty=true"><i class="fa fa-trash"></i></button></td>
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
export class QualityInspectionFormComponent implements OnInit {
  private qualityInspectionService = inject(QualityInspectionService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);

  saving = false;
  isDirty = false;
  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  form: any = {
    inspectionDate: new Date().toISOString().split('T')[0], inspectionType: 0,
    sampleSize: 1, itemId: '', batchNo: '',
    readings: [{ specification: '', minValue: null, maxValue: null, readingValue: '', isNumeric: true }]
  };

  ngOnInit(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName })))
    );
  }

  addReading() { this.form.readings.push({ specification: '', minValue: null, maxValue: null, readingValue: '', isNumeric: true }); this.isDirty = true; }

  save() {
    this.saving = true;
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      itemId: this.form.itemId || undefined,
      inspectionType: this.form.inspectionType,
      inspectionDate: this.form.inspectionDate,
      sampleSize: this.form.sampleSize || 1,
      batchNo: this.form.batchNo || undefined,
      readings: this.form.readings
        .filter((r: any) => r.specification)
        .map((r: any) => ({
          specification: r.specification, minValue: r.minValue, maxValue: r.maxValue,
          readingValue: r.readingValue || '', isNumeric: r.isNumeric ?? true,
        }))
    };
    this.qualityInspectionService.create(dto).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/inventory/quality-inspections']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}