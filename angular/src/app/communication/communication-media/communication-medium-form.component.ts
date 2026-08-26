import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { CommunicationMediumService } from '../../proxy/communication/communication-medium.service';
import { CommunicationMediumType } from '../../proxy/communication/communication-medium-type.enum';

@Component({
  selector: 'app-communication-medium-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
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
              <input type="text" class="form-control" formControlName="communicationChannel" placeholder="e.g. +603-12345678 or support@company.com">
            </div>
          </div>

          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Catch-All Employee Group ID</label>
              <input type="text" class="form-control" formControlName="catchAllEmployeeGroupId" placeholder="Optional fallback group GUID">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Provider Supplier ID</label>
              <input type="text" class="form-control" formControlName="providerSupplierId" placeholder="Optional supplier GUID (Twilio, SendGrid, etc.)">
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
                    <th>Handling Employee Group ID</th>
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
                        <input type="text" class="form-control form-control-sm" formControlName="employeeGroupId" placeholder="Employee Group GUID">
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
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  get timeslotsArray(): FormArray {
    return this.form.get('timeslots') as FormArray;
  }

  constructor() {
    this.form = this.fb.group({
      communicationMediumType: [CommunicationMediumType.Voice, Validators.required],
      communicationChannel: ['', Validators.maxLength(100)],
      catchAllEmployeeGroupId: [null],
      providerSupplierId: [null],
      isDisabled: [false],
      timeslots: this.fb.array([]),
    });
  }

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue({
          communicationMediumType: res.communicationMediumType,
          communicationChannel: res.communicationChannel,
          catchAllEmployeeGroupId: res.catchAllEmployeeGroupId,
          providerSupplierId: res.providerSupplierId,
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

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/communication/communication-media']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
