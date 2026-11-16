import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { IncomingCallSettingsService } from '../../proxy/telephony/incoming-call-settings.service';
import { CallRoutingMode } from '../../proxy/telephony/call-routing-mode.enum';
import { EmployeeGroupService } from '../../proxy/human-resources/employee-group.service';
import type { EmployeeGroupDto } from '../../proxy/human-resources/models';

@Component({
  selector: 'app-incoming-call-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
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
                    <th>Handling Employee Group</th>
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
                        <select class="form-select form-select-sm" formControlName="employeeGroupId">
                          <option value="">Select Employee Group...</option>
                          @for (g of employeeGroups(); track g.id) {
                            <option [value]="g.id">{{ g.groupName }}</option>
                          }
                        </select>
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

          <!-- Active Employee Group Test -->
          <div class="card mb-4 border-info">
            <div class="card-header bg-light py-2">
              <h6 class="mb-0 text-secondary">{{ '::CheckActiveGroup' | abpLocalization }}</h6>
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
                  <button type="button" class="btn btn-sm btn-outline-info" [disabled]="checkingActive()" (click)="checkActiveGroup()">
                    <i class="fa fa-phone-volume me-1"></i>{{ '::CheckActiveGroup' | abpLocalization }}
                  </button>
                </div>
                <div class="col-md-3">
                  @if (activeGroupLabel()) {
                    <span class="badge bg-info text-dark">{{ activeGroupLabel() }}</span>
                  }
                </div>
              </div>
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
  private employeeGroupService = inject(EmployeeGroupService);
  private toaster = inject(ToasterService);

  form: FormGroup;
  employeeGroups = signal<EmployeeGroupDto[]>([]);
  testDay = signal<number>(1);
  testTime = signal<string>('10:00');
  checkingActive = signal(false);
  activeGroupLabel = signal<string>('');

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
    this.employeeGroupService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: res => this.employeeGroups.set(res.items ?? []),
      error: () => {},
    });

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

  checkActiveGroup(): void {
    this.checkingActive.set(true);
    this.service.getActiveEmployeeGroup(this.testDay(), this.testTime()).subscribe({
      next: groupId => {
        this.checkingActive.set(false);
        const cleanId = (groupId ?? '').replace(/^"|"$/g, '');
        if (!cleanId) {
          this.activeGroupLabel.set('None');
          return;
        }
        const group = this.employeeGroups().find(g => g.id === cleanId);
        this.activeGroupLabel.set(group?.groupName ?? cleanId);
      },
      error: () => this.checkingActive.set(false),
    });
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
