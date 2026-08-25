import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { QualityFeedbackDocumentType } from '../../proxy/inventory/quality-feedback-document-type.enum';
import type { CreateQualityFeedbackDto, QualityFeedbackTemplateDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-quality-feedback-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewQualityFeedback' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row g-3 mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'DocumentType' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="documentType" (ngModelChange)="onDocumentTypeChange()">
              <option [ngValue]="0">User</option>
              <option [ngValue]="1">Customer</option>
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'DocumentName' | abpLocalization }} *</label>
            @if (documentType === 1) {
              <select class="form-select" [(ngModel)]="documentName">
                <option value="">-- {{ 'SelectCustomer' | abpLocalization }} --</option>
                @for (c of availableCustomers(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            } @else {
              <input class="form-control" [(ngModel)]="documentName" />
            }
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'QualityFeedbackTemplate' | abpLocalization }} *</label>
            <select class="form-select" [(ngModel)]="templateId" (ngModelChange)="onTemplateChange()">
              <option value="">-- Select --</option>
              @for (t of templates(); track t.id) {
                <option [value]="t.id">{{ t.templateName }}</option>
              }
            </select>
          </div>
          <div class="col-12">
            <label class="form-label">{{ '::Remarks' | abpLocalization }}</label>
            <textarea class="form-control" rows="2" [(ngModel)]="remarks"></textarea>
          </div>
        </div>

        @if (parameterRows.length > 0) {
          <div class="card bg-light mb-3">
            <div class="card-header py-2"><span class="fw-semibold">{{ 'Rating' | abpLocalization }}</span></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr><th>Parameter</th><th style="width:200px">{{ 'Rating' | abpLocalization }} (1-5)</th><th>{{ '::Remarks' | abpLocalization }}</th></tr></thead>
                <tbody>
                  @for (row of parameterRows; track row.parameter) {
                    <tr>
                      <td>{{ row.parameter }}</td>
                      <td>
                        <select class="form-select form-select-sm" [(ngModel)]="row.rating" [name]="'rating-' + row.parameter">
                          <option [ngValue]="1">1</option>
                          <option [ngValue]="2">2</option>
                          <option [ngValue]="3">3</option>
                          <option [ngValue]="4">4</option>
                          <option [ngValue]="5">5</option>
                        </select>
                      </td>
                      <td><input class="form-control form-control-sm" [(ngModel)]="row.remarks" [name]="'remarks-' + row.parameter" /></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <div class="d-flex justify-content-end gap-2">
          <a routerLink="/inventory/quality-feedback" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
          <button type="button" class="btn btn-primary btn-sm" [disabled]="!canSave() || saving" (click)="save()">
            <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
          </button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class QualityFeedbackFormComponent implements OnInit {
  private service = inject(QualityManagementService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);

  documentType: QualityFeedbackDocumentType = QualityFeedbackDocumentType.User;
  documentName = '';
  templateId = '';
  remarks = '';
  saving = false;

  templates = signal<QualityFeedbackTemplateDto[]>([]);
  availableCustomers = signal<{ id: string; name: string }[]>([]);
  parameterRows: { parameter: string; rating: number; remarks?: string }[] = [];

  ngOnInit() {
    this.service.getFeedbackTemplateList({ maxResultCount: 200 } as any).subscribe((r) => this.templates.set(r.items ?? []));
    this.customerService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' } as any).subscribe((r) =>
      this.availableCustomers.set((r.items ?? []).map((c: any) => ({ id: c.id, name: c.name || c.customerCode || c.id })))
    );
  }

  onDocumentTypeChange() {
    this.documentName = '';
  }

  onTemplateChange() {
    const template = this.templates().find((t) => t.id === this.templateId);
    this.parameterRows = (template?.parameters ?? []).map((p) => ({ parameter: p.parameter, rating: 3, remarks: '' }));
  }

  canSave(): boolean {
    return !!this.documentName && !!this.templateId;
  }

  save() {
    if (!this.canSave()) return;
    this.saving = true;
    const input: CreateQualityFeedbackDto = {
      companyId: this.companyContext.currentCompanyId() ?? '',
      documentType: this.documentType,
      documentName: this.documentName,
      templateId: this.templateId,
      remarks: this.remarks || undefined,
      parameters: this.parameterRows.map((r) => ({ parameter: r.parameter, rating: r.rating, remarks: r.remarks || undefined })),
    };
    this.service.createFeedback(input).subscribe({
      next: () => this.router.navigate(['/inventory/quality-feedback']),
      error: () => { this.saving = false; },
    });
  }
}
