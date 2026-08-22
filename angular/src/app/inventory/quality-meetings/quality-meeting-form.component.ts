import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { QualityMeetingDto } from '../../proxy/inventory/models';
import { QualityMeetingStatus } from '../../proxy/inventory/quality-meeting-status.enum';

@Component({
  selector: 'app-quality-meeting-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit ? 'QualityMeeting' : 'NewQualityMeeting') | abpLocalization">
      <div class="card">
        <div class="card-body">
          @if (isEdit) {
            <div class="row g-3 mb-3">
              <div class="col-md-4"><strong>{{ 'MeetingDate' | abpLocalization }}:</strong> {{ meeting?.meetingDate | date:'mediumDate' }}</div>
              <div class="col-md-4"><strong>{{ 'Chairperson' | abpLocalization }}:</strong> {{ meeting?.chairperson ?? '-' }}</div>
              <div class="col-md-4">
                <span class="badge" [ngClass]="meeting?.status === QualityMeetingStatus.Closed ? 'bg-secondary' : 'bg-success'">
                  {{ meeting?.status === QualityMeetingStatus.Closed ? 'Closed' : 'Open' }}
                </span>
              </div>
              <div class="col-12"><strong>{{ 'Attendees' | abpLocalization }}:</strong> {{ meeting?.attendees ?? '-' }}</div>
            </div>

            <div class="card bg-light mb-3">
              <div class="card-header py-2"><span class="fw-semibold">{{ 'Agendas' | abpLocalization }}</span></div>
              <div class="card-body p-0">
                <table class="table table-sm mb-0">
                  <tbody>
                    @for (a of meeting?.agendas; track a.id) {
                      <tr><td>{{ a.agenda }}</td></tr>
                    } @empty {
                      <tr><td class="text-muted">{{ '::NoData' | abpLocalization }}</td></tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <div class="card bg-light mb-3">
              <div class="card-header py-2"><span class="fw-semibold">{{ 'Minutes' | abpLocalization }}</span></div>
              <div class="card-body p-0">
                <table class="table table-sm mb-0">
                  <thead>
                    <tr>
                      <th>{{ 'Discussion' | abpLocalization }}</th>
                      <th>{{ 'ActionPlan' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (m of meeting?.minutes; track m.id) {
                      <tr><td>{{ m.discussion }}</td><td>{{ m.actionPlan ?? '-' }}</td></tr>
                    } @empty {
                      <tr><td colspan="2" class="text-muted">{{ '::NoData' | abpLocalization }}</td></tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <div class="d-flex justify-content-between align-items-center">
              <div>
                @if (meeting?.status === QualityMeetingStatus.Open) {
                  <button type="button" class="btn btn-danger btn-sm" (click)="closeMeeting()" [disabled]="isSaving">
                    <i class="fa fa-lock me-1"></i>{{ 'CloseMeeting' | abpLocalization }}
                  </button>
                }
              </div>
              <a routerLink="/inventory/quality-meetings" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
            </div>
          } @else {
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ 'MeetingDate' | abpLocalization }} *</label>
                  <input type="date" class="form-control form-control-sm" formControlName="meetingDate" />
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ 'Chairperson' | abpLocalization }}</label>
                  <input type="text" class="form-control form-control-sm" formControlName="chairperson" />
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ 'Attendees' | abpLocalization }}</label>
                  <input type="text" class="form-control form-control-sm" formControlName="attendees" />
                </div>
              </div>

              <div class="card bg-light mb-3">
                <div class="card-header d-flex justify-content-between align-items-center py-2">
                  <span class="fw-semibold">{{ 'Agendas' | abpLocalization }}</span>
                  <button type="button" class="btn btn-outline-primary btn-sm" (click)="addAgenda()">
                    <i class="fa fa-plus me-1"></i>{{ '::Add' | abpLocalization }}
                  </button>
                </div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody formArrayName="agendas">
                      @for (a of agendasArray.controls; track $index; let i = $index) {
                        <tr [formGroupName]="i">
                          <td><input type="text" class="form-control form-control-sm" formControlName="agenda" /></td>
                          <td style="width: 60px" class="text-center">
                            <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeAgenda(i)">
                              <i class="fa fa-trash"></i>
                            </button>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>

              <div class="card bg-light mb-3">
                <div class="card-header d-flex justify-content-between align-items-center py-2">
                  <span class="fw-semibold">{{ 'Minutes' | abpLocalization }}</span>
                  <button type="button" class="btn btn-outline-primary btn-sm" (click)="addMinute()">
                    <i class="fa fa-plus me-1"></i>{{ '::Add' | abpLocalization }}
                  </button>
                </div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <thead>
                      <tr>
                        <th>{{ 'Discussion' | abpLocalization }}</th>
                        <th>{{ 'ActionPlan' | abpLocalization }}</th>
                        <th style="width: 60px"></th>
                      </tr>
                    </thead>
                    <tbody formArrayName="minutes">
                      @for (m of minutesArray.controls; track $index; let i = $index) {
                        <tr [formGroupName]="i">
                          <td><input type="text" class="form-control form-control-sm" formControlName="discussion" /></td>
                          <td><input type="text" class="form-control form-control-sm" formControlName="actionPlan" /></td>
                          <td class="text-center">
                            <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeMinute(i)">
                              <i class="fa fa-trash"></i>
                            </button>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>

              <div class="d-flex justify-content-end gap-2">
                <a routerLink="/inventory/quality-meetings" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
                <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || isSaving">
                  <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
                </button>
              </div>
            </form>
          }
        </div>
      </div>
    </abp-page>
  `,
})
export class QualityMeetingFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(QualityManagementService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly QualityMeetingStatus = QualityMeetingStatus;

  form!: FormGroup;
  isEdit = false;
  isSaving = false;
  id: string | null = null;
  meeting: QualityMeetingDto | null = null;

  get agendasArray(): FormArray {
    return this.form.get('agendas') as FormArray;
  }

  get minutesArray(): FormArray {
    return this.form.get('minutes') as FormArray;
  }

  ngOnInit() {
    this.form = this.fb.group({
      companyId: ['00000000-0000-0000-0000-000000000000'],
      meetingDate: [new Date().toISOString().substring(0, 10), Validators.required],
      chairperson: [''],
      attendees: [''],
      agendas: this.fb.array([]),
      minutes: this.fb.array([]),
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id && this.id !== 'new') {
      this.isEdit = true;
      this.load();
    }
  }

  load() {
    if (!this.id) return;
    this.service.getMeeting(this.id).subscribe((res) => (this.meeting = res));
  }

  addAgenda() {
    this.agendasArray.push(this.fb.group({ agenda: ['', Validators.required] }));
  }

  removeAgenda(index: number) {
    this.agendasArray.removeAt(index);
  }

  addMinute() {
    this.minutesArray.push(this.fb.group({ discussion: ['', Validators.required], actionPlan: [''] }));
  }

  removeMinute(index: number) {
    this.minutesArray.removeAt(index);
  }

  save() {
    if (this.form.invalid) return;

    this.isSaving = true;
    const val = this.form.value;
    const input = {
      companyId: val.companyId,
      meetingDate: val.meetingDate,
      chairperson: val.chairperson,
      attendees: val.attendees,
      agendas: (val.agendas ?? []).map((a: { agenda: string }) => a.agenda),
      minutes: val.minutes ?? [],
    };

    this.service.createMeeting(input).subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/inventory/quality-meetings']);
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      },
    });
  }

  closeMeeting() {
    if (!this.id) return;
    this.isSaving = true;
    this.service.closeMeeting(this.id).subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('::MeetingClosed');
        this.load();
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? '::OperationFailed');
      },
    });
  }
}
