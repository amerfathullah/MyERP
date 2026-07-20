import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HolidayListService } from '../../proxy/human-resources';

@Component({
  selector: 'app-holiday-list-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewHolidayList' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Name' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.name" placeholder="e.g., Malaysia 2026" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Year' | abpLocalization }}</label>
            <input type="number" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.year" />
          </div>
          <div class="col-md-5">
            <label class="form-label">{{ 'WeeklyOff' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.weeklyOff" placeholder="Saturday,Sunday" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'Holidays' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Date' | abpLocalization }}</th><th>{{ 'Description' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (h of form.holidays; track $index) {
              <tr>
                <td><input type="date" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="h.holidayDate" /></td>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="h.description" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.holidays.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.holidays.push({holidayDate:'',description:''})">
          <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
        </button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/hr/holiday-lists">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class HolidayListFormComponent {
  private holidayListService = inject(HolidayListService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = { name: '', year: new Date().getFullYear(), weeklyOff: 'Saturday,Sunday', holidays: [{ holidayDate: '', description: '' }] };

  save() {
    this.saving = true;
    this.holidayListService.create(this.form)
      .subscribe({ next: () => this.router.navigate(['/hr/holiday-lists']), error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}