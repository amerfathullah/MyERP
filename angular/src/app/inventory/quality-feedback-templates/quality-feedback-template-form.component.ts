import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { CreateUpdateQualityFeedbackTemplateDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-quality-feedback-template-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'QualityFeedbackTemplate' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row g-3 mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
            <input class="form-control" [(ngModel)]="templateName" />
          </div>
        </div>

        <div class="card bg-light mb-3">
          <div class="card-header d-flex justify-content-between align-items-center py-2">
            <span class="fw-semibold">{{ 'AddParameter' | abpLocalization }}</span>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="parameters.push('')">
              <i class="fa fa-plus me-1"></i>{{ '::Add' | abpLocalization }}
            </button>
          </div>
          <div class="card-body p-0">
            <table class="table table-sm mb-0">
              <tbody>
                @for (p of parameters; track $index) {
                  <tr>
                    <td><input class="form-control form-control-sm" [(ngModel)]="parameters[$index]" [name]="'p'+$index" /></td>
                    <td class="text-center" style="width:60px">
                      <button type="button" class="btn btn-outline-danger btn-sm" (click)="parameters.splice($index,1)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
                @if (parameters.length === 0) {
                  <tr><td colspan="2" class="text-center text-muted py-3">No parameters yet.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a routerLink="/inventory/quality-feedback-templates" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
          <button type="button" class="btn btn-primary btn-sm" [disabled]="!templateName || saving" (click)="save()">
            <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
          </button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class QualityFeedbackTemplateFormComponent {
  private service = inject(QualityManagementService);
  private router = inject(Router);

  templateName = '';
  parameters: string[] = [];
  saving = false;

  save() {
    if (!this.templateName) return;
    this.saving = true;
    const input: CreateUpdateQualityFeedbackTemplateDto = {
      templateName: this.templateName,
      parameters: this.parameters.filter((p) => !!p),
    };
    this.service.createFeedbackTemplate(input).subscribe({
      next: () => this.router.navigate(['/inventory/quality-feedback-templates']),
      error: () => { this.saving = false; },
    });
  }
}
