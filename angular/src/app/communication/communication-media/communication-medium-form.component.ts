import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CommunicationMediumService } from '../../proxy/communication/communication-medium.service';
import { CommunicationMediumType } from '../../proxy/communication/communication-medium-type.enum';
import { EmployeeGroupService } from '../../proxy/human-resources/employee-group.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import type { EmployeeGroupDto } from '../../proxy/human-resources/models';
import type { SupplierDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-communication-medium-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Communication Medium</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Medium Type *</label>
              <select class="form-select" formControlName="communicationMediumType">
                <option [ngValue]="0">Voice</option>
                <option [ngValue]="1">Email</option>
                <option [ngValue]="2">Chat</option>
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Communication Channel</label>
              <input type="text" class="form-control" formControlName="communicationChannel" [placeholder]="'::Placeholder:CommunicationChannelExample' | abpLocalization">
            </div>
          </div>

          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Catch-All Employee Group</label>
              <select class="form-select" formControlName="catchAllEmployeeGroupId">
                <option value="">— None —</option>
                @for (g of employeeGroups(); track g.id) {
                  <option [value]="g.id">{{ g.groupName }}</option>
                }
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Provider Supplier</label>
              <select class="form-select" formControlName="providerSupplierId">
                <option value="">— None —</option>
                @for (s of suppliers(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              </select>
            </div>
          </div>

          <div class="form-check form-switch mb-4">
            <input class="form-check-input" type="checkbox" id="isDisabled" formControlName="isDisabled">
            <label class="form-check-label" for="isDisabled">Disabled</label>
          </div>

          <!-- Timeslots section -->
          <div class="card mb-4 border">
            <div class="card-header bg-light d-flex justify-content-between align-items-center py-2">
              <h6 class="mb-0 text-secondary">Operating Timeslots</h6>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="addTimeslot()">+ Add Timeslot</button>
            </div>
            <div class="card-body p-0">
              <table class="table table-bordered mb-0">
                <thead class="table-light small">
                  <tr>
                    <th>Day of Week</th>
                    <th>From Time</th>
                    <th>To Time</th>
                    <th>Handling Employee Group</th>
                    <th style="width: 80px;" class="text-center">Action</th>
                  </tr>
                </thead>
                <tbody formArrayName="timeslots">
                  @for (slot of timeslotsArray.controls; track $index; let i = $index) {
                    <tr [formGroupName]="i">
                      <td>
                        <select class="form-select form-select-sm" formControlName="dayOfWeek">
                          <option [ngValue]="1">Monday</option>
                          <option [ngValue]="2">Tuesday</option>
                          <option [ngValue]="3">Wednesday</option>
                          <option [ngValue]="4">Thursday</option>
                          <option [ngValue]="5">Friday</option>
                          <option [ngValue]="6">Saturday</option>
                          <option [ngValue]="0">Sunday</option>
                        </select>
                      </td>
                      <td>
                        <input type="time" class="form-control form-control-sm" formControlName="fromTime">
                      </td>
                      <td>
                        <input type="time" class="form-control form-control-sm" formControlName="toTime">
                      </td>
                      <td>
                        <select class="form-select form-select-sm" formControlName="employeeGroupId">
                          <option value="">Select Employee Group...</option>
                          @for (g of employeeGroups(); track g.id) {
                            <option [value]="g.id">{{ g.groupName }}</option>
                          }
                        </select>
                      </td>
                      <td class="text-center">
                        <button type="button" class="btn btn-sm btn-outline-danger" (click)="removeTimeslot(i)">×</button>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="5" class="text-center text-muted py-3 small">No timeslots configured. All communications will route to catch-all group.</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          @if (isEdit && id) {
            <div class="card mb-4 border-info">
              <div class="card-header bg-light py-2">
                <h6 class="mb-0 text-secondary">{{ '::CheckRouting' | abpLocalization }}</h6>
              </div>
              <div class="card-body">
                <div class="row g-2 align-items-end">
                  <div class="col-md-3">
                    <label class="form-label small">Day of Week</label>
                    <select class="form-select form-select-sm" [ngModel]="testDay()" [ngModelOptions]="{standalone: true}" (ngModelChange)="testDay.set($event)">
                      <option [ngValue]="1">Monday</option>
                      <option [ngValue]="2">Tuesday</option>
                      <option [ngValue]="3">Wednesday</option>
                      <option [ngValue]="4">Thursday</option>
                      <option [ngValue]="5">Friday</option>
                      <option [ngValue]="6">Saturday</option>
                      <option [ngValue]="0">Sunday</option>
                    </select>
                  </div>
                  <div class="col-md-3">
                    <label class="form-label small">Time</label>
                    <input type="time" class="form-control form-control-sm" [ngModel]="testTime()" [ngModelOptions]="{standalone: true}" (ngModelChange)="testTime.set($event)">
                  </div>
                  <div class="col-md-3">
                    <button type="button" class="btn btn-sm btn-outline-info" [disabled]="checkingRouting()" (click)="checkHandlingGroup()">
                      <i class="fa fa-route me-1"></i>{{ '::CheckRouting' | abpLocalization }}
                    </button>
                  </div>
                  <div class="col-md-3">
                    @if (resolvedGroupLabel()) {
                      <span class="badge bg-info text-dark">{{ resolvedGroupLabel() }}</span>
                    }
                  </div>
                </div>
              </div>
            </div>
          }

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/communication/communication-media" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class CommunicationMediumFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(CommunicationMediumService);
  private employeeGroupService = inject(EmployeeGroupService);
  private supplierService = inject(SupplierService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;
  employeeGroups = signal<EmployeeGroupDto[]>([]);
  suppliers = signal<SupplierDto[]>([]);
  testDay = signal<number>(1);
  testTime = signal<string>('10:00');
  checkingRouting = signal(false);
  resolvedGroupLabel = signal<string>('');

  get timeslotsArray(): FormArray {
    return this.form.get('timeslots') as FormArray;
  }

  constructor() {
    this.form = this.fb.group({
      communicationMediumType: [CommunicationMediumType.Voice, Validators.required],
      communicationChannel: ['', Validators.maxLength(100)],
      catchAllEmployeeGroupId: [''],
      providerSupplierId: [''],
      isDisabled: [false],
      timeslots: this.fb.array([]),
    });
  }

  ngOnInit() {
    this.employeeGroupService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: res => this.employeeGroups.set(res.items ?? []),
      error: () => {},
    });
    this.supplierService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: res => this.suppliers.set(res.items ?? []),
      error: () => {},
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue({
          communicationMediumType: res.communicationMediumType,
          communicationChannel: res.communicationChannel,
          catchAllEmployeeGroupId: res.catchAllEmployeeGroupId ?? '',
          providerSupplierId: res.providerSupplierId ?? '',
          isDisabled: res.isDisabled,
        });

        this.timeslotsArray.clear();
        if (res.timeslots) {
          for (const slot of res.timeslots) {
            this.timeslotsArray.push(this.fb.group({
              dayOfWeek: [slot.dayOfWeek],
              fromTime: [slot.fromTime],
              toTime: [slot.toTime],
              employeeGroupId: [slot.employeeGroupId, Validators.required],
            }));
          }
        }
      });
    }
  }

  addTimeslot() {
    this.timeslotsArray.push(this.fb.group({
      dayOfWeek: [1, Validators.required],
      fromTime: ['09:00', Validators.required],
      toTime: ['17:00', Validators.required],
      employeeGroupId: ['', Validators.required],
    }));
  }

  removeTimeslot(index: number) {
    this.timeslotsArray.removeAt(index);
  }

  checkHandlingGroup(): void {
    if (!this.id) return;
    this.checkingRouting.set(true);
    this.service.getHandlingEmployeeGroup(this.id, this.testDay(), this.testTime()).subscribe({
      next: groupId => {
        this.checkingRouting.set(false);
        const cleanId = (groupId ?? '').replace(/^"|"$/g, '');
        if (!cleanId) {
          this.resolvedGroupLabel.set('None');
          return;
        }
        const group = this.employeeGroups().find(g => g.id === cleanId);
        this.resolvedGroupLabel.set(group?.groupName ?? cleanId);
      },
      error: () => this.checkingRouting.set(false),
    });
  }

  save() {
    if (this.form.invalid) return;
    const raw = this.form.value;
    const payload = {
      ...raw,
      catchAllEmployeeGroupId: raw.catchAllEmployeeGroupId || null,
      providerSupplierId: raw.providerSupplierId || null,
    };
    const req = this.isEdit
      ? this.service.update(this.id!, payload)
      : this.service.create(payload);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/communication/communication-media']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
