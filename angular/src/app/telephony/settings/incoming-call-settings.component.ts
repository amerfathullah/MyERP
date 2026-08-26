import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { IncomingCallSettingsService } from '../../proxy/telephony/incoming-call-settings.service';
import { CallRoutingMode } from '../../proxy/telephony/call-routing-mode.enum';

@Component({
  selector: 'app-incoming-call-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">Incoming Call Settings</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Call Routing Mode *</label>
              <select class="form-select" formControlName="callRouting">
                <option [ngValue]="0">Sequential (Agent after agent)</option>
                <option [ngValue]="1">Simultaneous (Ring all available agents)</option>
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Greeting Message</label>
              <input type="text" class="form-control" formControlName="greetingMessage" placeholder="Welcome message played to caller">
            </div>
          </div>

          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Agent Busy Message</label>
              <input type="text" class="form-control" formControlName="agentBusyMessage" placeholder="Played when all agents are on call">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Agent Unavailable Message</label>
              <input type="text" class="form-control" formControlName="agentUnavailableMessage" placeholder="Played when outside operating hours">
            </div>
          </div>

          <!-- Schedule table -->
          <div class="card mb-4 border">
            <div class="card-header bg-light d-flex justify-content-between align-items-center py-2">
              <h6 class="mb-0 text-secondary">Incoming Call Handling Schedule</h6>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="addSchedule()">+ Add Schedule</button>
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
                <tbody formArrayName="schedules">
                  @for (slot of schedulesArray.controls; track $index; let i = $index) {
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
                        <button type="button" class="btn btn-sm btn-outline-danger" (click)="removeSchedule(i)">×</button>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="5" class="text-center text-muted py-3 small">No weekly handling schedules configured.</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save Settings</button>
        </form>
      </div>
    </div>
  `
})
export class IncomingCallSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(IncomingCallSettingsService);
  private toaster = inject(ToasterService);

  form: FormGroup;

  get schedulesArray(): FormArray {
    return this.form.get('schedules') as FormArray;
  }

  constructor() {
    this.form = this.fb.group({
      callRouting: [CallRoutingMode.Sequential, Validators.required],
      greetingMessage: ['', Validators.maxLength(500)],
      agentBusyMessage: ['', Validators.maxLength(500)],
      agentUnavailableMessage: ['', Validators.maxLength(500)],
      schedules: this.fb.array([]),
    });
  }

  ngOnInit() {
    this.service.get().subscribe(res => {
      this.form.patchValue({
        callRouting: res.callRouting,
        greetingMessage: res.greetingMessage,
        agentBusyMessage: res.agentBusyMessage,
        agentUnavailableMessage: res.agentUnavailableMessage,
      });

      this.schedulesArray.clear();
      if (res.schedules) {
        for (const s of res.schedules) {
          this.schedulesArray.push(this.fb.group({
            dayOfWeek: [s.dayOfWeek],
            fromTime: [s.fromTime],
            toTime: [s.toTime],
            employeeGroupId: [s.employeeGroupId, Validators.required],
          }));
        }
      }
    });
  }

  addSchedule() {
    this.schedulesArray.push(this.fb.group({
      dayOfWeek: [1, Validators.required],
      fromTime: ['09:00', Validators.required],
      toTime: ['18:00', Validators.required],
      employeeGroupId: ['', Validators.required],
    }));
  }

  removeSchedule(index: number) {
    this.schedulesArray.removeAt(index);
  }

  save() {
    if (this.form.invalid) return;
    this.service.update(this.form.value).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
