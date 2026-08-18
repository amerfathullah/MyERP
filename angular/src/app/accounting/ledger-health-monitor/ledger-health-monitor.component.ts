import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LedgerHealthMonitorService } from '../../proxy/accounting/ledger-health-monitor.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { LedgerHealthRecordDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-ledger-health-monitor',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid mt-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4><i class="fas fa-heart-pulse me-2"></i>{{ 'LedgerHealthMonitor' | abpLocalization }}</h4>
      </div>

      <div class="card mb-3">
        <div class="card-header"><h6 class="mb-0">{{ 'Settings' | abpLocalization }}</h6></div>
        <div class="card-body">
          <div class="row g-3 align-items-end">
            <div class="col-md-3">
              <div class="form-check form-switch mt-2">
                <input class="form-check-input" type="checkbox" id="enabled" [(ngModel)]="settings.isEnabled" />
                <label class="form-check-label" for="enabled">{{ 'Enabled' | abpLocalization }}</label>
              </div>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'LookbackPeriodDays' | abpLocalization }}</label>
              <input type="number" class="form-control form-control-sm" [(ngModel)]="settings.lookbackPeriodDays" min="1" />
            </div>
            <div class="col-md-3">
              <button class="btn btn-primary btn-sm w-100" (click)="saveSettings()" [disabled]="isSaving()">
                <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
              </button>
            </div>
            <div class="col-md-3">
              <button class="btn btn-warning btn-sm w-100" (click)="runCheck()" [disabled]="isRunning()">
                <i class="fas fa-play me-1"></i>{{ 'RunCheckNow' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (lastRunHealthy() !== null) {
        <div class="alert" [class.alert-success]="lastRunHealthy()" [class.alert-danger]="!lastRunHealthy()">
          @if (lastRunHealthy()) {
            <i class="fa fa-check-circle me-1"></i>{{ 'LedgerIsHealthy' | abpLocalization }}
          } @else {
            <i class="fa fa-exclamation-triangle me-1"></i>{{ 'LedgerIssuesDetected' | abpLocalization }}
          }
        </div>
      }

      <div class="card">
        <div class="card-header"><h6 class="mb-0">{{ 'DetectedIssues' | abpLocalization }}</h6></div>
        <div class="card-body p-0">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (records().length === 0) {
            <div class="text-center py-4 text-muted">{{ 'NoIssuesDetectedYet' | abpLocalization }}</div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'CheckedAt' | abpLocalization }}</th>
                <th>{{ 'CheckType' | abpLocalization }}</th>
                <th>{{ 'Severity' | abpLocalization }}</th>
                <th>{{ 'Description' | abpLocalization }}</th>
                <th>{{ 'Difference' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (r of records(); track r.id) {
                  <tr>
                    <td>{{ r.checkedAt | date:'dd/MM/yyyy HH:mm' }}</td>
                    <td>{{ r.checkType }}</td>
                    <td><span class="badge" [class.bg-danger]="r.severity === 'Critical'" [class.bg-warning]="r.severity !== 'Critical'">{{ r.severity }}</span></td>
                    <td class="small">{{ r.description }}</td>
                    <td>{{ r.difference ?? '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `,
})
export class LedgerHealthMonitorComponent implements OnInit {
  private service = inject(LedgerHealthMonitorService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  settings = { isEnabled: false, lookbackPeriodDays: 30 };
  records = signal<LedgerHealthRecordDto[]>([]);
  isLoading = signal(false);
  isSaving = signal(false);
  isRunning = signal(false);
  lastRunHealthy = signal<boolean | null>(null);

  ngOnInit(): void {
    this.loadSettings();
    this.loadRecords();
  }

  private get companyId(): string | null {
    return this.companyContext.currentCompanyId();
  }

  loadSettings(): void {
    const cid = this.companyId;
    if (!cid) return;
    this.service.getSettings({ companyId: cid }).subscribe(s => {
      this.settings = { isEnabled: s.isEnabled ?? false, lookbackPeriodDays: s.lookbackPeriodDays ?? 30 };
    });
  }

  loadRecords(): void {
    const cid = this.companyId;
    if (!cid) return;
    this.isLoading.set(true);
    this.service.getRecords({ companyId: cid, maxResultCount: 50, skipCount: 0 } as any).subscribe({
      next: r => { this.records.set(r.items ?? []); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  saveSettings(): void {
    const cid = this.companyId;
    if (!cid) return;
    this.isSaving.set(true);
    this.service.updateSettings({
      companyId: cid,
      isEnabled: this.settings.isEnabled,
      lookbackPeriodDays: this.settings.lookbackPeriodDays,
    }).subscribe({
      next: () => { this.isSaving.set(false); this.toaster.success('::SuccessfullySaved'); },
      error: () => this.isSaving.set(false),
    });
  }

  runCheck(): void {
    const cid = this.companyId;
    if (!cid) return;
    this.isRunning.set(true);
    this.service.runCheck({ companyId: cid }).subscribe({
      next: result => {
        this.isRunning.set(false);
        this.lastRunHealthy.set(result.isHealthy ?? true);
        this.loadRecords();
      },
      error: () => this.isRunning.set(false),
    });
  }
}
