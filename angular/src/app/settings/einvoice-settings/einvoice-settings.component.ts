import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { EInvoiceSettingsService } from '../../proxy/einvoice/einvoice-settings.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-einvoice-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'EInvoiceSettings' | abpLocalization">
      <!-- Connection Status Card -->
      <div class="card mb-3">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h6 class="mb-0"><i class="fa fa-plug me-2"></i>{{ 'ConnectionStatus' | abpLocalization }}</h6>
          @if (status()) {
            <span class="badge" [class]="status()!.isConnected ? 'bg-success' : status()!.isTokenExpired ? 'bg-warning' : 'bg-secondary'">
              {{ status()!.isConnected ? 'Connected' : status()!.isTokenExpired ? 'Token Expired' : 'Not Connected' }}
            </span>
          }
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div></div>
          } @else if (status()) {
            <div class="row g-3">
              <div class="col-md-3">
                <small class="text-muted d-block">{{ 'Environment' | abpLocalization }}</small>
                <span class="badge" [class]="status()!.environment === 'Production' ? 'bg-danger' : 'bg-info'">
                  {{ status()!.environment }}
                </span>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">{{ 'Credentials' | abpLocalization }}</small>
                <span>{{ status()!.isConfigured ? '✓ Configured' : '✗ Not configured' }}</span>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">{{ 'Certificate' | abpLocalization }}</small>
                <span>{{ status()!.isCertificateConfigured ? '✓ Uploaded' : '✗ Not uploaded' }}</span>
              </div>
              <div class="col-md-3">
                <small class="text-muted d-block">Token Expires</small>
                <span>{{ status()!.tokenExpiresAt ? (status()!.tokenExpiresAt | date:'dd/MM/yyyy HH:mm') : '—' }}</span>
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Credentials Form -->
      <div class="card mb-3">
        <div class="card-header"><h6 class="mb-0"><i class="fa fa-key me-2"></i>{{ 'APICredentials' | abpLocalization }}</h6></div>
        <div class="card-body">
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'Environment' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="credentials.environment">
                <option value="Sandbox">Sandbox (Testing)</option>
                <option value="Production">Production</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">Client ID</label>
              <input type="text" class="form-control" [(ngModel)]="credentials.clientId" placeholder="LHDN Client ID">
            </div>
            <div class="col-md-4">
              <label class="form-label">Client Secret</label>
              <input type="password" class="form-control" [(ngModel)]="credentials.clientSecret" placeholder="Leave blank to keep existing">
            </div>
          </div>
          <div class="mt-3 d-flex gap-2">
            <button class="btn btn-primary" (click)="saveCredentials()" [disabled]="isSaving()">
              <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
            </button>
            <button class="btn btn-success" (click)="connect()" [disabled]="isConnecting() || !credentials.clientId">
              @if (isConnecting()) {
                <span class="spinner-border spinner-border-sm me-1"></span>
              } @else {
                <i class="fa fa-plug me-1"></i>
              }
              {{ 'Connect' | abpLocalization }}
            </button>
          </div>
        </div>
      </div>

      <!-- Digital Certificate -->
      <div class="card mb-3">
        <div class="card-header"><h6 class="mb-0"><i class="fa fa-certificate me-2"></i>{{ 'DigitalCertificate' | abpLocalization }}</h6></div>
        <div class="card-body">
          <p class="text-muted small mb-3">Upload PFX/P12 certificate for XAdES document signing. Required for LHDN submission.</p>
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">Certificate File (.pfx / .p12)</label>
              <input type="file" class="form-control" accept=".pfx,.p12" (change)="onCertificateSelected($event)">
            </div>
            <div class="col-md-4">
              <label class="form-label">Certificate Password</label>
              <input type="password" class="form-control" [(ngModel)]="certPassword" placeholder="PFX password">
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button class="btn btn-primary w-100" (click)="uploadCertificate()" [disabled]="!certBase64 || isUploading()">
                <i class="fa fa-upload me-1"></i>Upload
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- TIN Lookup -->
      <div class="card">
        <div class="card-header"><h6 class="mb-0"><i class="fa fa-search me-2"></i>{{ 'TINLookup' | abpLocalization }}</h6></div>
        <div class="card-body">
          <p class="text-muted small mb-3">Search LHDN database for taxpayer TIN by ID type and value.</p>
          <div class="row g-3">
            <div class="col-md-3">
              <label class="form-label">ID Type</label>
              <select class="form-select" [(ngModel)]="searchIdType">
                <option value="BRN">Business Registration (BRN)</option>
                <option value="NRIC">MyKad (NRIC)</option>
                <option value="PASSPORT">Passport</option>
                <option value="ARMY">Army ID</option>
              </select>
            </div>
            <div class="col-md-5">
              <label class="form-label">ID Value</label>
              <input type="text" class="form-control" [(ngModel)]="searchIdValue" placeholder="e.g. 202001234567">
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button class="btn btn-outline-primary w-100" (click)="searchTaxpayer()" [disabled]="!searchIdValue || isSearching()">
                <i class="fa fa-search me-1"></i>Search
              </button>
            </div>
            <div class="col-md-2 d-flex align-items-end">
              @if (searchResult()) {
                @if (searchResult()!.isSuccess) {
                  <div class="text-success">
                    <strong>{{ searchResult()!.tin }}</strong><br>
                    <small>{{ searchResult()!.name }}</small>
                  </div>
                } @else {
                  <div class="text-danger small">{{ searchResult()!.errorMessage }}</div>
                }
              }
            </div>
          </div>
        </div>
      </div>
    </abp-page>
  `,
})
export class EInvoiceSettingsComponent implements OnInit {
  private einvoiceService = inject(EInvoiceSettingsService);
  private toaster = inject(ToasterService);

  status = signal<any>(null);
  isLoading = signal(true);
  isSaving = signal(false);
  isConnecting = signal(false);
  isUploading = signal(false);
  isSearching = signal(false);
  searchResult = signal<any>(null);

  credentials = { clientId: '', clientSecret: '', environment: 'Sandbox' };
  certBase64 = '';
  certPassword = '';
  searchIdType = 'BRN';
  searchIdValue = '';

  ngOnInit() {
    this.loadStatus();
  }

  loadStatus() {
    this.isLoading.set(true);
    this.einvoiceService.getConnectionStatus().subscribe({
      next: (res) => {
        this.status.set(res);
        if (res.clientId) this.credentials.clientId = res.clientId;
        if (res.environment) this.credentials.environment = res.environment;
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  saveCredentials() {
    this.isSaving.set(true);
    this.einvoiceService.saveCredentials(this.credentials as any).subscribe({
      next: () => {
        this.toaster.success('Credentials saved successfully');
        this.isSaving.set(false);
        this.loadStatus();
      },
      error: () => this.isSaving.set(false),
    });
  }

  connect() {
    this.isConnecting.set(true);
    this.einvoiceService.connect().subscribe({
      next: (res: any) => {
        if (res.isSuccess) {
          this.toaster.success('Connected to LHDN successfully');
        } else {
          this.toaster.error(res.errorMessage || 'Connection failed');
        }
        this.isConnecting.set(false);
        this.loadStatus();
      },
      error: () => this.isConnecting.set(false),
    });
  }

  onCertificateSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = (reader.result as string).split(',')[1];
      this.certBase64 = base64;
    };
    reader.readAsDataURL(file);
  }

  uploadCertificate() {
    this.isUploading.set(true);
    this.einvoiceService.saveCertificate({
      certificateBase64: this.certBase64,
      certificatePassword: this.certPassword || null,
    } as any).subscribe({
      next: () => {
        this.toaster.success('Certificate uploaded successfully');
        this.isUploading.set(false);
        this.certBase64 = '';
        this.certPassword = '';
        this.loadStatus();
      },
      error: () => this.isUploading.set(false),
    });
  }

  searchTaxpayer() {
    this.isSearching.set(true);
    this.searchResult.set(null);
    this.einvoiceService.searchTaxpayer(this.searchIdType, this.searchIdValue).subscribe({
      next: (res) => { this.searchResult.set(res); this.isSearching.set(false); },
      error: () => this.isSearching.set(false),
    });
  }
}
