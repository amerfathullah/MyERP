import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { EInvoiceSettingsService } from '../proxy/einvoice/einvoice-settings.service';
import type { EInvoiceConnectionStatusDto } from '../proxy/einvoice/models';

@Component({
  selector: 'app-einvoice-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'EInvoiceSettings' | abpLocalization">
      <!-- Connection status card -->
      <div class="card mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h6 class="mb-0"><i class="fa fa-plug me-2"></i>LHDN MyInvois Connection</h6>
          <button class="btn btn-outline-primary btn-sm" (click)="loadStatus()" [disabled]="statusLoading()">
            @if (statusLoading()) { <i class="fa fa-spinner fa-spin me-1"></i> }
            Refresh Status
          </button>
        </div>
        <div class="card-body">
          @if (status()) {
            <div class="row g-2">
              <div class="col-auto">
                <span class="badge" [class.bg-success]="status()!.isConnected" [class.bg-danger]="!status()!.isConnected">
                  <i class="fa" [class.fa-check-circle]="status()!.isConnected" [class.fa-times-circle]="!status()!.isConnected"></i>
                  {{ status()!.isConnected ? 'Connected' : 'Disconnected' }}
                </span>
              </div>
              <div class="col-auto">
                <span class="badge bg-secondary">{{ status()!.environment || 'Unknown' }}</span>
              </div>
              @if (status()!.tokenExpiresAt) {
                <div class="col-auto small text-muted">
                  Token expires: {{ status()!.tokenExpiresAt | date:'dd/MM/yyyy HH:mm' }}
                  @if (status()!.isTokenExpired) { <span class="badge bg-danger ms-1">Expired</span> }
                </div>
              }
              @if (status()!.isCertificateConfigured) {
                <div class="col-auto"><span class="badge bg-success"><i class="fa fa-certificate me-1"></i>Certificate OK</span></div>
              } @else {
                <div class="col-auto"><span class="badge bg-warning text-dark"><i class="fa fa-exclamation me-1"></i>No Certificate</span></div>
              }
            </div>
            <div class="mt-3">
              <button class="btn btn-success btn-sm" (click)="testConnect()" [disabled]="connecting()">
                @if (connecting()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                Test Connection
              </button>
            </div>
          }
        </div>
      </div>

      <!-- Credentials -->
      <div class="card mb-4">
        <div class="card-header"><h6 class="mb-0"><i class="fa fa-key me-2"></i>API Credentials</h6></div>
        <div class="card-body">
          <form [formGroup]="credForm" (ngSubmit)="saveCredentials()">
            <div class="row g-3">
              <div class="col-md-5">
                <label class="form-label">Client ID *</label>
                <input class="form-control font-monospace" formControlName="clientId" placeholder="MyInvois Client ID" />
              </div>
              <div class="col-md-5">
                <label class="form-label">Client Secret</label>
                <input type="password" class="form-control font-monospace" formControlName="clientSecret" placeholder="Leave blank to keep existing" />
              </div>
              <div class="col-md-2">
                <label class="form-label">Environment</label>
                <select class="form-select" formControlName="environment">
                  <option value="Sandbox">Sandbox</option>
                  <option value="Production">Production</option>
                </select>
              </div>
            </div>
            <div class="mt-3">
              <button type="submit" class="btn btn-primary btn-sm" [disabled]="credForm.invalid || savingCreds()">
                @if (savingCreds()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                Save Credentials
              </button>
            </div>
          </form>
        </div>
      </div>

      <!-- Certificate -->
      <div class="card mb-4">
        <div class="card-header"><h6 class="mb-0"><i class="fa fa-certificate me-2"></i>Digital Certificate (X.509)</h6></div>
        <div class="card-body">
          <form [formGroup]="certForm" (ngSubmit)="saveCertificate()">
            <div class="row g-3">
              <div class="col-12">
                <label class="form-label">Certificate (Base64 PFX/P12) *</label>
                <textarea class="form-control font-monospace" formControlName="certificateBase64" rows="4"
                  placeholder="Paste base64-encoded PFX certificate here..."></textarea>
              </div>
              <div class="col-md-4">
                <label class="form-label">Certificate Password</label>
                <input type="password" class="form-control" formControlName="certificatePassword" />
              </div>
            </div>
            <div class="mt-3">
              <button type="submit" class="btn btn-primary btn-sm" [disabled]="certForm.invalid || savingCert()">
                @if (savingCert()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                Upload Certificate
              </button>
            </div>
          </form>
        </div>
      </div>

      <!-- TIN Lookup -->
      <div class="card">
        <div class="card-header"><h6 class="mb-0"><i class="fa fa-search me-2"></i>Taxpayer TIN Lookup</h6></div>
        <div class="card-body">
          <div class="row g-3">
            <div class="col-md-3">
              <label class="form-label">ID Type</label>
              <select class="form-select" [(ngModel)]="tinIdType">
                <option value="BRN">BRN (Business Registration)</option>
                <option value="NRIC">NRIC (National ID)</option>
                <option value="PASSPORT">Passport</option>
                <option value="ARMY">Army ID</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">ID Value</label>
              <input class="form-control" [(ngModel)]="tinIdValue" placeholder="e.g. 202301012345" />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button class="btn btn-outline-primary w-100" (click)="lookupTin()" [disabled]="!tinIdValue || tinSearching()">
                @if (tinSearching()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                Lookup
              </button>
            </div>
          </div>
          @if (tinResult()) {
            <div class="mt-3 p-3 rounded"
              [class.bg-success-subtle]="tinResult()!.isSuccess"
              [class.bg-danger-subtle]="!tinResult()!.isSuccess">
              @if (tinResult()!.isSuccess) {
                <div><strong>TIN:</strong> <code>{{ tinResult()!.tin }}</code></div>
                <div><strong>Name:</strong> {{ tinResult()!.name }}</div>
              } @else {
                <div class="text-danger">{{ tinResult()!.errorMessage }}</div>
              }
            </div>
          }
        </div>
      </div>

      <div class="mt-3">
        <a class="btn btn-secondary" routerLink="/einvoice">← Back to Submissions</a>
      </div>
    </abp-page>
  `,
})
export class EInvoiceSettingsComponent implements OnInit {
  private settingsService = inject(EInvoiceSettingsService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);

  status = signal<EInvoiceConnectionStatusDto | null>(null);
  statusLoading = signal(false);
  connecting = signal(false);
  savingCreds = signal(false);
  savingCert = signal(false);
  tinSearching = signal(false);
  tinResult = signal<any>(null);
  tinIdType = 'BRN';
  tinIdValue = '';

  credForm = this.fb.group({
    clientId: ['', Validators.required],
    clientSecret: [''],
    environment: ['Sandbox', Validators.required],
  });

  certForm = this.fb.group({
    certificateBase64: ['', Validators.required],
    certificatePassword: [''],
  });

  ngOnInit(): void { this.loadStatus(); }

  loadStatus(): void {
    this.statusLoading.set(true);
    this.settingsService.getConnectionStatus().subscribe({
      next: s => { this.status.set(s); this.statusLoading.set(false); },
      error: () => this.statusLoading.set(false),
    });
  }

  testConnect(): void {
    this.connecting.set(true);
    this.settingsService.connect().subscribe({
      next: r => {
        if (r.isSuccess) {
          this.toaster.success('Connection successful!');
          this.loadStatus();
        } else {
          this.toaster.error(r.errorMessage ?? 'Connection failed');
        }
        this.connecting.set(false);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? 'Connection failed');
        this.connecting.set(false);
      },
    });
  }

  saveCredentials(): void {
    if (this.credForm.invalid) return;
    this.savingCreds.set(true);
    const val = this.credForm.getRawValue() as any;
    this.settingsService.saveCredentials(val).subscribe({
      next: () => { this.toaster.success('Credentials saved'); this.savingCreds.set(false); },
      error: (err: any) => { this.toaster.error(err?.error?.error?.message ?? 'Save failed'); this.savingCreds.set(false); },
    });
  }

  saveCertificate(): void {
    if (this.certForm.invalid) return;
    this.savingCert.set(true);
    const val = this.certForm.getRawValue() as any;
    this.settingsService.saveCertificate(val).subscribe({
      next: () => { this.toaster.success('Certificate uploaded'); this.savingCert.set(false); this.loadStatus(); },
      error: (err: any) => { this.toaster.error(err?.error?.error?.message ?? 'Upload failed'); this.savingCert.set(false); },
    });
  }

  lookupTin(): void {
    if (!this.tinIdValue) return;
    this.tinSearching.set(true);
    this.tinResult.set(null);
    this.settingsService.searchTaxpayer(this.tinIdType, this.tinIdValue).subscribe({
      next: r => { this.tinResult.set(r); this.tinSearching.set(false); },
      error: (err: any) => {
        this.tinResult.set({ isSuccess: false, errorMessage: err?.error?.error?.message ?? 'Lookup failed' });
        this.tinSearching.set(false);
      },
    });
  }
}
