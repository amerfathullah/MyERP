import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { CreateOperationDto, OperationDto } from '../../proxy/manufacturing/models';

@Component({
  selector: 'app-operation-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? 'EditOperation' : 'NewOperation') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Name' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.name" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'WorkstationType' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.workstationType" [placeholder]="'::Placeholder:WorkstationType' | abpLocalization" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Workstation' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.workstationId">
              <option [ngValue]="null">—</option>
              @for (ws of workstations; track ws.id) {
                <option [ngValue]="ws.id">{{ ws.name }}</option>
              }
            </select>
          </div>
        </div>

        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'Description' | abpLocalization }}</label>
            <textarea class="form-control" rows="2" (ngModelChange)="isDirty=true" [(ngModel)]="form.description"></textarea>
          </div>
          <div class="col-md-6 d-flex flex-column justify-content-end gap-2">
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="opBatch" (ngModelChange)="isDirty=true" [(ngModel)]="form.createJobCardBasedOnBatchSize" />
              <label class="form-check-label" for="opBatch">{{ 'CreateJobCardBasedOnBatchSize' | abpLocalization }}</label>
            </div>
            @if (form.createJobCardBasedOnBatchSize) {
              <input type="number" class="form-control form-control-sm" style="width:150px" min="1" (ngModelChange)="isDirty=true" [(ngModel)]="form.batchSize" [placeholder]="'BatchSize' | abpLocalization" />
            }
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="opCorrective" (ngModelChange)="isDirty=true" [(ngModel)]="form.isCorrectiveOperation" />
              <label class="form-check-label" for="opCorrective">{{ 'IsCorrectiveOperation' | abpLocalization }}</label>
            </div>
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="opActive" (ngModelChange)="isDirty=true" [(ngModel)]="form.isActive" />
              <label class="form-check-label" for="opActive">{{ 'Active' | abpLocalization }}</label>
            </div>
          </div>
        </div>

        <h6 class="mb-2">{{ 'SubOperations' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr>
            <th>{{ 'Operation' | abpLocalization }}</th>
            <th>{{ 'OperationTime' | abpLocalization }}</th>
            <th>{{ 'Description' | abpLocalization }}</th>
            <th></th>
          </tr></thead>
          <tbody>
            @for (s of form.subOperations; track $index) {
              <tr>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="s.operationId">
                    @for (o of allOperations; track o.id) {
                      <option [ngValue]="o.id">{{ o.name }}</option>
                    }
                  </select>
                </td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="s.timeInMins" /></td>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="s.description" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.subOperations.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addSubOperation()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-between">
          <span class="fw-bold">{{ 'TotalOperationTime' | abpLocalization }}: {{ getTotalTime() | number:'1.0-2' }}</span>
          <div class="d-flex gap-2">
            <a class="btn btn-secondary" routerLink="/manufacturing/operations">{{ 'Cancel' | abpLocalization }}</a>
            <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class OperationFormComponent implements OnInit {
  private service = inject(ManufacturingService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  saving = false;
  isDirty = false;
  isEdit = false;
  operationId: string | null = null;
  workstations: any[] = [];
  allOperations: OperationDto[] = [];
  form: CreateOperationDto & { subOperations: any[] } = {
    name: '', description: null, workstationId: null, workstationType: null,
    createJobCardBasedOnBatchSize: false, batchSize: 1, isCorrectiveOperation: false,
    isActive: true, subOperations: [],
  };

  ngOnInit(): void {
    this.service.getWorkstationList({ skipCount: 0, maxResultCount: 1000, sorting: '' } as any)
      .subscribe((r) => this.workstations = r.items ?? []);
    this.service.getOperationList({ skipCount: 0, maxResultCount: 1000, sorting: '' })
      .subscribe((r) => this.allOperations = r.items ?? []);

    this.operationId = this.route.snapshot.paramMap.get('id');
    if (this.operationId) {
      this.isEdit = true;
      this.service.getOperation(this.operationId).subscribe((op) => {
        this.form = {
          name: op.name!, description: op.description ?? null,
          workstationId: op.workstationId ?? null, workstationType: op.workstationType ?? null,
          createJobCardBasedOnBatchSize: op.createJobCardBasedOnBatchSize ?? false,
          batchSize: op.batchSize ?? 1, isCorrectiveOperation: op.isCorrectiveOperation ?? false,
          isActive: op.isActive ?? true,
          subOperations: (op.subOperations ?? []).map(s => ({ operationId: s.operationId, timeInMins: s.timeInMins, description: s.description })),
        };
      });
    }
  }

  addSubOperation() {
    this.form.subOperations.push({ operationId: this.operationId ?? null, timeInMins: 0, description: '' });
  }

  getTotalTime(): number {
    return (this.form.subOperations ?? []).reduce((s, o) => s + (o.timeInMins || 0), 0);
  }

  save() {
    this.saving = true;
    const request = this.isEdit && this.operationId
      ? this.service.updateOperation(this.operationId, this.form)
      : this.service.createOperation(this.form);
    request.subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/manufacturing/operations']); },
      error: () => { this.saving = false; },
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}
