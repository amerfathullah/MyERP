import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { CallLogService } from '../../proxy/telephony/call-log.service';
import { TelephonyCallTypeService } from '../../proxy/telephony/telephony-call-type.service';
import { TelephonyCallTypeDto } from '../../proxy/telephony/models';

@Component({
  selector: 'app-call-log-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Call Log</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-4 mb-3">
              <label class="form-label">Call ID *</label>
              <input type="text" class="form-control" formControlName="callId" [readonly]="isEdit" placeholder="Unique provider Call ID">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">From (Caller) *</label>
              <input type="text" class="form-control" formControlName="from" placeholder="+60123456789">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">To (Receiver) *</label>
              <input type="text" class="form-control" formControlName="to" placeholder="+60388889999">
            </div>
          </div>

          <div class="row">
            <div class="col-md-4 mb-3">
              <label class="form-label">Direction *</label>
              <select class="form-select" formControlName="callDirection">
                <option [ngValue]="0">Incoming</option>
                <option [ngValue]="1">Outgoing</option>
              </select>
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">Status *</label>
              <select class="form-select" formControlName="status">
                <option [ngValue]="0">Ringing</option>
                <option [ngValue]="1">In Progress</option>
                <option [ngValue]="2">Completed</option>
                <option [ngValue]="3">Failed</option>
                <option [ngValue]="4">Busy</option>
                <option [ngValue]="5">No Answer</option>
                <option [ngValue]="6">Queued</option>
                <option [ngValue]="7">Cancelled</option>
              </select>
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">Call Type</label>
              <select class="form-select" formControlName="telephonyCallTypeId">
                <option [ngValue]="null">-- None --</option>
                @for (type of callTypes; track type.id) {
                  <option [ngValue]="type.id">{{ type.callTypeName }}</option>
                }
              </select>
            </div>
          </div>

          <div class="row">
            <div class="col-md-4 mb-3">
              <label class="form-label">Duration (seconds)</label>
              <input type="number" class="form-control" formControlName="duration">
            </div>

            <div class="col-md-8 mb-3">
              <label class="form-label">Recording URL</label>
              <input type="text" class="form-control" formControlName="recordingUrl" placeholder="https://...">
            </div>
          </div>

          <div class="row">
            <div class="col-md-4 mb-3">
              <label class="form-label">Customer ID</label>
              <input type="text" class="form-control" formControlName="customerId" placeholder="Optional customer GUID">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">Received By Employee ID</label>
              <input type="text" class="form-control" formControlName="callReceivedByEmployeeId" placeholder="Optional employee GUID">
            </div>

            <div class="col-md-4 mb-3">
              <label class="form-label">Medium</label>
              <input type="text" class="form-control" formControlName="medium" placeholder="e.g. Twilio, Exotel">
            </div>
          </div>

          <div class="mb-4">
            <label class="form-label">Call Summary / Notes</label>
            <textarea class="form-control" rows="4" formControlName="summary" placeholder="Notes from the call..."></textarea>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/telephony/call-logs" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class CallLogFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(CallLogService);
  private callTypeService = inject(TelephonyCallTypeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;
  callTypes: TelephonyCallTypeDto[] = [];

  constructor() {
    this.form = this.fb.group({
      callId: ['', [Validators.required, Validators.maxLength(128)]],
      from: ['', [Validators.required, Validators.maxLength(50)]],
      to: ['', [Validators.required, Validators.maxLength(50)]],
      callDirection: [0, Validators.required],
      status: [0, Validators.required],
      duration: [0],
      recordingUrl: ['', Validators.maxLength(1024)],
      medium: ['', Validators.maxLength(100)],
      customerId: [null],
      callReceivedByEmployeeId: [null],
      telephonyCallTypeId: [null],
      summary: ['', Validators.maxLength(4000)],
    });
  }

  ngOnInit() {
    this.callTypeService.getList({ maxResultCount: 100 } as any).subscribe(res => {
      this.callTypes = res.items ?? [];
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue({
          callId: res.callId,
          from: res.from,
          to: res.to,
          callDirection: res.callDirection,
          status: res.status,
          duration: res.duration,
          recordingUrl: res.recordingUrl,
          medium: res.medium,
          customerId: res.customerId,
          callReceivedByEmployeeId: res.callReceivedByEmployeeId,
          telephonyCallTypeId: res.telephonyCallTypeId,
          summary: res.summary,
        });
      });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/telephony/call-logs']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
