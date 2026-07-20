import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe , RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-email-template-list',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'EmailTemplates' | abpLocalization">
      <!-- Create/Edit Form -->
      @if (editMode) {
        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">{{ editing ? 'Edit Template' : 'New Template' }}</h6>
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'Name' | abpLocalization }}</label>
              <input type="text" class="form-control" [(ngModel)]="formData.name" [disabled]="!!editing">
            </div>
            <div class="col-md-4">
              <label class="form-label">Document Type</label>
              <select class="form-select" [(ngModel)]="formData.documentType">
                <option value="">Any</option>
                <option value="SalesInvoice">Sales Invoice</option>
                <option value="PurchaseInvoice">Purchase Invoice</option>
                <option value="SalesOrder">Sales Order</option>
                <option value="DeliveryNote">Delivery Note</option>
                <option value="PaymentEntry">Payment Entry</option>
                <option value="Dunning">Dunning</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">Subject</label>
              <input type="text" class="form-control" [(ngModel)]="formData.subject" [placeholder]="'Use {variable} for placeholders'">
            </div>
            <div class="col-12">
              <label class="form-label">Body</label>
              <textarea class="form-control" [(ngModel)]="formData.body" rows="8" [placeholder]="'HTML supported. Use {customer}, {amount}, {invoice_no} etc.'"></textarea>
            </div>
          </div>
          <div class="d-flex gap-2 mt-3">
            <button class="btn btn-primary btn-sm" (click)="saveTemplate()" [disabled]="!formData.name || !formData.subject || !formData.body">
              <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
            </button>
            <button class="btn btn-outline-info btn-sm" (click)="previewTemplate()" [disabled]="!editing">
              <i class="fa fa-eye me-1"></i>Preview
            </button>
            <button class="btn btn-outline-secondary btn-sm" (click)="cancelEdit()">{{ 'Cancel' | abpLocalization }}</button>
          </div>
        </div></div>
      }

      @if (previewHtml) {
        <div class="card mb-3 border-info"><div class="card-body">
          <h6 class="card-title text-info"><i class="fa fa-eye me-1"></i>Preview</h6>
          <p class="fw-bold">Subject: {{ previewSubject }}</p>
          <hr>
          <div [innerHTML]="previewHtml"></div>
        </div></div>
      }

      @if (!editMode) {
        <div class="d-flex justify-content-end mb-3">
          <button class="btn btn-primary btn-sm" (click)="startCreate()">
            <i class="fa fa-plus me-1"></i>New Template
          </button>
        </div>
      }

      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && templates.length === 0 && !editMode) {
        <div class="text-center py-5">
          <i class="fa fa-envelope-open-text fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">No email templates yet. Create templates for automated notifications.</p>
        </div>
      } @else if (!isLoading && templates.length > 0) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>Subject</th>
              <th>Document Type</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (t of templates; track t.id) {
                <tr>
                  <td class="fw-medium">{{ t.name }}</td>
                  <td class="text-truncate" style="max-width:300px">{{ t.subject }}</td>
                  <td><span class="badge bg-light text-dark">{{ t.documentType ?? 'Any' }}</span></td>
                  <td class="text-end">
                    <div class="btn-group btn-group-sm">
                      <button class="btn btn-outline-primary" (click)="startEdit(t)"><i class="fa fa-edit"></i></button>
                      <button class="btn btn-outline-danger" (click)="deleteTemplate(t.id)"><i class="fa fa-trash"></i></button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `
})
export class EmailTemplateListComponent implements OnInit {
  private restService = inject(RestService);
  private toaster = inject(ToasterService);

  templates: any[] = [];
  isLoading = false;
  editMode = false;
  editing: any = null;
  formData: any = { name: '', subject: '', body: '', documentType: '' };
  previewHtml = '';
  previewSubject = '';

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.restService.request<any, any[]>({ method: 'GET', url: '/api/app/email-template' }, { apiName: 'Default' }).subscribe({
      next: res => { this.templates = res ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  startCreate() {
    this.editMode = true;
    this.editing = null;
    this.formData = { name: '', subject: '', body: '', documentType: '' };
    this.previewHtml = '';
  }

  startEdit(t: any) {
    this.editMode = true;
    this.editing = t;
    this.formData = { ...t };
    this.previewHtml = '';
  }

  cancelEdit() {
    this.editMode = false;
    this.editing = null;
    this.previewHtml = '';
  }

  saveTemplate() {
    if (this.editing) {
      this.restService.request<any, void>({ method: 'PUT', url: `/api/app/email-template/${this.editing.id}`, body: this.formData }, { apiName: 'Default' }).subscribe({
        next: () => { this.toaster.success('Template updated'); this.cancelEdit(); this.loadData(); }
      });
    } else {
      this.restService.request<any, void>({ method: 'POST', url: '/api/app/email-template', body: this.formData }, { apiName: 'Default' }).subscribe({
        next: () => { this.toaster.success('Template created'); this.cancelEdit(); this.loadData(); }
      });
    }
  }

  previewTemplate() {
    if (!this.editing) return;
    const sampleVars = {
      customer: 'Sample Customer Sdn Bhd',
      invoice_no: 'SI-2026-00001',
      amount: '5,000.00',
      company_name: 'My Company',
      due_date: '2026-08-15',
      days: '30'
    };
    this.restService.request<any, any>({ method: 'POST', url: `/api/app/email-template/${this.editing.id}/preview`, body: sampleVars }, { apiName: 'Default' }).subscribe({
      next: res => { this.previewSubject = res.subject; this.previewHtml = res.body; }
    });
  }

  deleteTemplate(id: string) {
    if (!confirm('Delete this template?')) return;
    this.restService.request<any, void>({ method: 'DELETE', url: `/api/app/email-template/${id}` }, { apiName: 'Default' }).subscribe({ next: () => this.loadData() });
  }
}
