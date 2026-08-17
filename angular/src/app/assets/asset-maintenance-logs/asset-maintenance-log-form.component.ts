import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetMaintenanceLogService } from '../../proxy/assets/asset-maintenance-log.service';
import { AssetMaintenanceStatus } from '../../proxy/maintenance/asset-maintenance-status.enum';
import type { AssetMaintenanceLogDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-maintenance-log-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'AssetMaintenanceLog' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetMaintenanceLog' | abpLocalization }}</h5>
          <a class="btn btn-outline-secondary btn-sm" routerLink="/assets/maintenance-logs">
            <i class="fa fa-arrow-left me-1"></i>{{ 'Back' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (log()) {
            <div class="row mb-4 p-3 bg-light rounded">
              <div class="col-md-3">
                <small class="text-muted d-block">{{ 'Asset' | abpLocalization }}</small>
                <strong>{{ log()!.assetName || log()!.assetId }}</strong>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">{{ 'Task' | abpLocalization }}</small>
                <strong>{{ log()!.maintenanceTask }}</strong>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">{{ 'DueDate' | abpLocalization }}</small>
                <strong>{{ log()!.dueDate | date:'dd/MM/yyyy' }}</strong>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
                @switch (log()!.status) {
                  @case (AssetMaintenanceStatus.Planned) {
                    <span class="badge bg-primary">{{ 'Planned' | abpLocalization }}</span>
                  }
                  @case (AssetMaintenanceStatus.Completed) {
                    <span class="badge bg-success">{{ 'Completed' | abpLocalization }}</span>
                  }
                  @case (AssetMaintenanceStatus.Cancelled) {
                    <span class="badge bg-secondary">{{ 'Cancelled' | abpLocalization }}</span>
                  }
                  @case (AssetMaintenanceStatus.Overdue) {
                    <span class="badge bg-danger">{{ 'Overdue' | abpLocalization }}</span>
                  }
                }
              </div>
            </div>

            @if (log()!.status === AssetMaintenanceStatus.Planned || log()!.status === AssetMaintenanceStatus.Overdue) {
              <form [formGroup]="form" (ngSubmit)="completeLog()">
                <div class="row mb-3">
                  <div class="col-md-6">
                    <label class="form-label required">{{ 'CompletionDate' | abpLocalization }}</label>
                    <input type="date" class="form-control" formControlName="completionDate" />
                  </div>
                  <div class="col-md-6">
                    <label class="form-label">{{ 'Cost' | abpLocalization }}</label>
                    <input type="number" step="0.01" class="form-control" formControlName="cost" placeholder="0.00" />
                  </div>
                </div>

                <div class="mb-3">
                  <label class="form-label">{{ 'ActionsPerformed' | abpLocalization }}</label>
                  <textarea class="form-control" rows="3" formControlName="actionsPerformed" placeholder="Details of work done"></textarea>
                </div>

                <div class="row mb-3">
                  <div class="col-md-6">
                    <div class="form-check mt-4">
                      <input type="checkbox" class="form-check-input" id="hasCert" formControlName="hasCertificate" />
                      <label class="form-check-label" for="hasCert">{{ 'HasCertificate' | abpLocalization }}</label>
                    </div>
                  </div>
                  <div class="col-md-6">
                    <label class="form-label">{{ 'CertificateDetails' | abpLocalization }}</label>
                    <input type="text" class="form-control" formControlName="certificateDetails" placeholder="Certificate reference" />
                  </div>
                </div>

                <div class="mb-3">
                  <label class="form-label">{{ 'Remarks' | abpLocalization }}</label>
                  <input type="text" class="form-control" formControlName="remarks" />
                </div>

                <div class="d-flex justify-content-end gap-2 mt-4">
                  <a class="btn btn-outline-secondary" routerLink="/assets/maintenance-logs">{{ 'Cancel' | abpLocalization }}</a>
                  <button type="submit" class="btn btn-success" [disabled]="form.invalid || isSaving()">
                    @if (isSaving()) {
                      <i class="fa fa-spinner fa-spin me-1"></i>
                    }
                    <i class="fa fa-check me-1"></i>{{ 'CompleteTask' | abpLocalization }}
                  </button>
                </div>
              </form>
            } @else {
              <div class="row mb-3">
                <div class="col-md-6">
                  <label class="form-label">{{ 'CompletionDate' | abpLocalization }}</label>
                  <input type="text" class="form-control" [value]="log()!.completionDate ? (log()!.completionDate | date:'dd/MM/yyyy') : '—'" readonly />
                </div>
                <div class="col-md-6">
                  <label class="form-label">{{ 'Cost' | abpLocalization }}</label>
                  <input type="text" class="form-control" [value]="log()!.cost ? (log()!.cost | number:'1.2-2') : '—'" readonly />
                </div>
              </div>
              <div class="mb-3">
                <label class="form-label">{{ 'ActionsPerformed' | abpLocalization }}</label>
                <textarea class="form-control" rows="3" readonly>{{ log()!.actionsPerformed || '—' }}</textarea>
              </div>
            }
          }
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetMaintenanceLogFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(AssetMaintenanceLogService);
  private toaster = inject(ToasterService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  AssetMaintenanceStatus = AssetMaintenanceStatus;
  log = signal<AssetMaintenanceLogDto | null>(null);
  isSaving = signal(false);
  logId = signal<string | null>(null);

  form: FormGroup = this.fb.group({
    completionDate: [new Date().toISOString().substring(0, 10), Validators.required],
    cost: [null],
    actionsPerformed: [''],
    hasCertificate: [false],
    certificateDetails: [''],
    remarks: [''],
  });

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.logId.set(id);
      this.loadLog(id);
    }
  }

  loadLog(id: string) {
    this.service.get(id).subscribe((res) => {
      this.log.set(res);
      if (res.completionDate) {
        this.form.patchValue({
          completionDate: res.completionDate.substring(0, 10),
          cost: res.cost,
          actionsPerformed: res.actionsPerformed,
          hasCertificate: res.hasCertificate,
          certificateDetails: res.certificateDetails,
          remarks: res.remarks,
        });
      }
    });
  }

  completeLog() {
    if (this.form.invalid) return;
    this.isSaving.set(true);

    const val = this.form.value;
    this.service
      .complete(this.logId()!, {
        completionDate: new Date(val.completionDate).toISOString(),
        cost: val.cost ? Number(val.cost) : null,
        actionsPerformed: val.actionsPerformed || null,
        hasCertificate: !!val.hasCertificate,
        certificateDetails: val.certificateDetails || null,
        remarks: val.remarks || null,
      })
      .subscribe({
        next: () => {
          this.toaster.success('::MaintenanceCompleted');
          this.router.navigate(['/assets/maintenance-logs']);
        },
        error: (err) => {
          this.isSaving.set(false);
          this.toaster.error(err?.error?.error?.message ?? 'Complete failed');
        },
      });
  }
}
