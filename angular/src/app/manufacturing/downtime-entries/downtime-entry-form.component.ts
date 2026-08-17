import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DowntimeEntryService } from '../../proxy/manufacturing/downtime-entry.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

const STOP_REASONS = [
  'Excessive machine set up time',
  'Unplanned machine maintenance',
  'On-machine press checks',
  'Machine operator errors',
  'Machine malfunction',
  'Electricity down',
  'Other',
];

@Component({
  selector: 'app-downtime-entry-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'Edit' : 'NewDowntimeEntry') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Workstation' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.workstationId">
              <option value="">-- {{ 'Select' | abpLocalization }} --</option>
              @for (w of workstations(); track w.id) { <option [value]="w.id">{{ w.name }}</option> }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Operator' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.operatorId">
              <option value="">-- {{ 'Select' | abpLocalization }} --</option>
              @for (e of employees(); track e.id) { <option [value]="e.id">{{ e.name }}</option> }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'StopReason' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.stopReason">
              @for (r of stopReasons; track r) { <option [value]="r">{{ r }}</option> }
            </select>
          </div>
        </div>

        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'FromTime' | abpLocalization }}</label>
            <input type="datetime-local" class="form-control" [(ngModel)]="form.fromTime" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ToTime' | abpLocalization }}</label>
            <input type="datetime-local" class="form-control" [(ngModel)]="form.toTime" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Downtime' | abpLocalization }} (min)</label>
            <input type="text" class="form-control" [value]="computedDowntime()" disabled />
          </div>
        </div>

        <div class="mb-3">
          <label class="form-label">{{ 'Remarks' | abpLocalization }}</label>
          <textarea class="form-control" rows="3" [(ngModel)]="form.remarks"></textarea>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/manufacturing/downtime-entries">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving()"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class DowntimeEntryFormComponent implements OnInit {
  private service = inject(DowntimeEntryService);
  private manufacturingService = inject(ManufacturingService);
  private employeeService = inject(EmployeeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  stopReasons = STOP_REASONS;
  saving = signal(false);
  isEdit = signal(false);
  private entryId: string | null = null;

  workstations = signal<{ id: string; name: string }[]>([]);
  employees = signal<{ id: string; name: string }[]>([]);

  form: any = { workstationId: '', operatorId: '', fromTime: '', toTime: '', stopReason: STOP_REASONS[0], remarks: '' };

  ngOnInit(): void {
    this.manufacturingService.getWorkstationList({ maxResultCount: 500 } as any).subscribe(r =>
      this.workstations.set((r.items ?? []).map((w: any) => ({ id: w.id, name: w.name }))));
    this.employeeService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.employees.set((r.items ?? []).map((e: any) => ({ id: e.id, name: e.fullName ?? `${e.firstName} ${e.lastName ?? ''}`.trim() }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.entryId = id;
      this.service.get(id).subscribe(e => {
        this.form = {
          workstationId: e.workstationId, operatorId: e.operatorId,
          fromTime: e.fromTime ? e.fromTime.substring(0, 16) : '',
          toTime: e.toTime ? e.toTime.substring(0, 16) : '',
          stopReason: e.stopReason, remarks: e.remarks ?? '',
        };
      });
    }
  }

  computedDowntime(): string {
    if (!this.form.fromTime || !this.form.toTime) return '—';
    const mins = (new Date(this.form.toTime).getTime() - new Date(this.form.fromTime).getTime()) / 60000;
    return mins >= 0 ? mins.toFixed(1) : '—';
  }

  save(): void {
    this.saving.set(true);
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      workstationId: this.form.workstationId,
      operatorId: this.form.operatorId,
      fromTime: this.form.fromTime,
      toTime: this.form.toTime,
      stopReason: this.form.stopReason,
      remarks: this.form.remarks || null,
    };
    const req = this.entryId ? this.service.update(this.entryId, dto) : this.service.create(dto);
    req.subscribe({
      next: () => {
        this.toaster.success(this.entryId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        this.router.navigate(['/manufacturing/downtime-entries']);
      },
      error: () => this.saving.set(false),
    });
  }
}
