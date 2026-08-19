import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { CreateRoutingDto, OperationDto, WorkstationDto } from '../../proxy/manufacturing/models';

@Component({
  selector: 'app-routing-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? 'EditRouting' : 'NewRouting') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'Name' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.name" />
          </div>
          <div class="col-md-6 d-flex align-items-end">
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="rtDisabled" (ngModelChange)="isDirty=true" [(ngModel)]="form.isDisabled" />
              <label class="form-check-label" for="rtDisabled">{{ 'Disabled' | abpLocalization }}</label>
            </div>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Operations' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr>
            <th>{{ 'Sequence' | abpLocalization }}</th>
            <th>{{ 'Operation' | abpLocalization }}</th>
            <th>{{ 'Workstation' | abpLocalization }}</th>
            <th>{{ 'OperationTime' | abpLocalization }}</th>
            <th></th>
          </tr></thead>
          <tbody>
            @for (row of form.operations; track $index) {
              <tr>
                <td><input type="number" class="form-control form-control-sm" style="width:80px" (ngModelChange)="isDirty=true" [(ngModel)]="row.sequenceId" /></td>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="row.operationId">
                    @for (o of operations; track o.id) {
                      <option [ngValue]="o.id">{{ o.name }}</option>
                    }
                  </select>
                </td>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="row.workstationId">
                    <option [ngValue]="null">—</option>
                    @for (ws of workstations; track ws.id) {
                      <option [ngValue]="ws.id">{{ ws.name }}</option>
                    }
                  </select>
                </td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="row.timeInMins" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.operations!.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addOperation()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/manufacturing/routings">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class RoutingFormComponent implements OnInit {
  private service = inject(ManufacturingService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  saving = false;
  isDirty = false;
  isEdit = false;
  routingId: string | null = null;
  operations: OperationDto[] = [];
  workstations: WorkstationDto[] = [];
  form: CreateRoutingDto = { name: '', isDisabled: false, operations: [] };

  ngOnInit(): void {
    this.service.getOperationList({ skipCount: 0, maxResultCount: 1000, sorting: '' })
      .subscribe((r) => this.operations = r.items ?? []);
    this.service.getWorkstationList({ skipCount: 0, maxResultCount: 1000, sorting: '' } as any)
      .subscribe((r) => this.workstations = r.items ?? []);

    this.routingId = this.route.snapshot.paramMap.get('id');
    if (this.routingId) {
      this.isEdit = true;
      this.service.getRouting(this.routingId).subscribe((rt) => {
        this.form = {
          name: rt.name!,
          isDisabled: rt.isDisabled ?? false,
          operations: (rt.operations ?? []).map(o => ({
            operationId: o.operationId!, sequenceId: o.sequenceId ?? 0,
            timeInMins: o.timeInMins ?? 0, workstationId: o.workstationId ?? null,
          })),
        };
      });
    }
  }

  addOperation() {
    const nextSeq = ((this.form.operations ?? []).reduce((m, o) => Math.max(m, o.sequenceId ?? 0), 0)) + 10;
    this.form.operations!.push({ operationId: this.operations[0]?.id ?? '', sequenceId: nextSeq, timeInMins: 0, workstationId: null });
  }

  save() {
    this.saving = true;
    const request = this.isEdit && this.routingId
      ? this.service.updateRouting(this.routingId, this.form)
      : this.service.createRouting(this.form);
    request.subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/manufacturing/routings']); },
      error: () => { this.saving = false; },
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}
