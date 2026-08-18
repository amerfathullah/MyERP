import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { EmailDigestService } from '../../proxy/core/email-digest.service';
import { emailDigestFrequencyOptions } from '../../proxy/core/email-digest-frequency.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { EmailDigestSendResultDto } from '../../proxy/core/models';

@Component({
  selector: 'app-email-digest',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-envelope me-2"></i>{{ 'MyERP::EmailDigest' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <form (ngSubmit)="save()">
            <div class="row mb-3">
              <div class="col-md-3">
                <div class="form-check form-switch mt-4">
                  <input type="checkbox" class="form-check-input" id="enabled" [(ngModel)]="settings.isEnabled" name="isEnabled" />
                  <label class="form-check-label" for="enabled">{{ 'MyERP::Enabled' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'MyERP::Frequency' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="settings.frequency" name="frequency">
                  @for (o of frequencyOptions; track o.value) { <option [ngValue]="o.value">{{ o.key }}</option> }
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'MyERP::Recipients' | abpLocalization }}</label>
                <input type="text" class="form-control" [(ngModel)]="settings.recipients" name="recipients"
                  [placeholder]="'MyERP::RecipientsPlaceholder' | abpLocalization" />
                <small class="form-text text-muted">{{ 'MyERP::RecipientsHelp' | abpLocalization }}</small>
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::DigestContent' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="includeSo" [(ngModel)]="settings.includeOpenSalesOrders" name="includeSo" />
                  <label class="form-check-label" for="includeSo">{{ 'MyERP::OpenSalesOrders' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="includeOverdue" [(ngModel)]="settings.includeOverdueInvoices" name="includeOverdue" />
                  <label class="form-check-label" for="includeOverdue">{{ 'MyERP::OverdueInvoices' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="includeLowStock" [(ngModel)]="settings.includeLowStockItems" name="includeLowStock" />
                  <label class="form-check-label" for="includeLowStock">{{ 'MyERP::LowStockItems' | abpLocalization }}</label>
                </div>
              </div>
            </div>

            @if (lastSentAt()) {
              <p class="text-muted small">{{ 'MyERP::LastSentAt' | abpLocalization }}: {{ lastSentAt() | date:'dd/MM/yyyy HH:mm' }}</p>
            }

            <div class="d-flex justify-content-end gap-2 mt-4">
              <button type="button" class="btn btn-outline-secondary" [disabled]="sending()" (click)="sendNow()">
                @if (sending()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'MyERP::SendNow' | abpLocalization }}
              </button>
              <button type="submit" class="btn btn-primary" [disabled]="saving()">
                <i class="bi bi-check-lg me-1"></i>{{ 'MyERP::Save' | abpLocalization }}
              </button>
            </div>

            @if (lastResult()) {
              <div class="alert alert-info mt-3 mb-0">
                {{ 'MyERP::DigestSentSummary' | abpLocalization }}:
                {{ lastResult()!.recipientCount }} {{ 'MyERP::Recipients' | abpLocalization }},
                {{ lastResult()!.openSalesOrderCount }} SO, {{ lastResult()!.overdueInvoiceCount }} {{ 'MyERP::OverdueInvoices' | abpLocalization }},
                {{ lastResult()!.lowStockItemCount }} {{ 'MyERP::LowStockItems' | abpLocalization }}.
              </div>
            }
          </form>
        }
      </div>
    </div>
  `,
})
export class EmailDigestComponent implements OnInit {
  private service = inject(EmailDigestService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  frequencyOptions = emailDigestFrequencyOptions;

  settings = {
    isEnabled: false,
    frequency: 1,
    recipients: '',
    includeOpenSalesOrders: true,
    includeOverdueInvoices: true,
    includeLowStockItems: true,
  };
  lastSentAt = signal<string | null>(null);
  lastResult = signal<EmailDigestSendResultDto | null>(null);

  loading = signal(true);
  saving = signal(false);
  sending = signal(false);

  private get companyId(): string | null {
    return this.companyContext.currentCompanyId();
  }

  ngOnInit(): void {
    const cid = this.companyId;
    if (!cid) { this.loading.set(false); return; }
    this.service.getSettings({ companyId: cid }).subscribe({
      next: (s) => {
        this.settings = {
          isEnabled: s.isEnabled ?? false,
          frequency: s.frequency ?? 1,
          recipients: s.recipients ?? '',
          includeOpenSalesOrders: s.includeOpenSalesOrders ?? true,
          includeOverdueInvoices: s.includeOverdueInvoices ?? true,
          includeLowStockItems: s.includeLowStockItems ?? true,
        };
        this.lastSentAt.set(s.lastSentAt ?? null);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  save(): void {
    const cid = this.companyId;
    if (!cid) return;
    this.saving.set(true);
    this.service.updateSettings({ companyId: cid, ...this.settings }).subscribe({
      next: () => { this.saving.set(false); this.toaster.success('MyERP::SuccessfullySaved'); },
      error: () => this.saving.set(false),
    });
  }

  sendNow(): void {
    const cid = this.companyId;
    if (!cid) return;
    this.sending.set(true);
    this.service.sendNow({ companyId: cid }).subscribe({
      next: (result) => {
        this.sending.set(false);
        this.lastResult.set(result);
        this.lastSentAt.set(new Date().toISOString());
        this.toaster.success('MyERP::DigestSent');
      },
      error: () => this.sending.set(false),
    });
  }
}
