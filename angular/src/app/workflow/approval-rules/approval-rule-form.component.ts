import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { ApprovalWorkflowService } from '../../proxy/workflow/approval-workflow.service';
import type { CreateApprovalRuleDto } from '../../proxy/workflow/dtos/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-approval-rule-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, LocalizationPipe, PageModule],
  templateUrl: './approval-rule-form.component.html',
  styleUrls: ['./approval-rule-form.component.scss'],
})
export class ApprovalRuleFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(ApprovalWorkflowService);
  private toaster = inject(ToasterService);

  documentTypes = ['SalesInvoice', 'PurchaseInvoice', 'PurchaseOrder', 'PaymentEntry', 'JournalEntry', 'StockEntry'];

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    documentType: ['SalesInvoice', Validators.required],
    level: [1, [Validators.required, Validators.min(1)]],
    approverRoleName: [''],
    approverUserId: [''],
    minimumAmount: [null as number | null],
    conditionExpression: [''],
    description: [''],
    isActive: [true],
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const dto = this.form.getRawValue() as unknown as CreateApprovalRuleDto;
    this.service.createRule(dto).subscribe({
      next: () => {
        this.toaster.success('Approval Rule created');
        this.router.navigate(['/workflow/rules']);
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed to create'),
    });
  }

  cancel(): void {
    this.router.navigate(['/workflow/rules']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}