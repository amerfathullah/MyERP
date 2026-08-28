import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { ApprovalWorkflowService } from '../../proxy/workflow/approval-workflow.service';
import type { CreateApprovalRuleDto, UpdateApprovalRuleDto } from '../../proxy/workflow/dtos/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-approval-rule-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, LocalizationPipe, PageModule],
  templateUrl: './approval-rule-form.component.html',
  styleUrls: ['./approval-rule-form.component.scss'],
})
export class ApprovalRuleFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(ApprovalWorkflowService);
  private toaster = inject(ToasterService);

  documentTypes = ['SalesInvoice', 'PurchaseInvoice', 'PurchaseOrder', 'PaymentEntry', 'JournalEntry', 'StockEntry'];

  ruleId: string | null = null;
  get isEditMode(): boolean { return !!this.ruleId; }

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

  ngOnInit(): void {
    this.ruleId = this.route.snapshot.paramMap.get('id');
    if (this.ruleId) {
      this.service.getRule(this.ruleId).subscribe({
        next: (rule) => {
          this.form.patchValue(rule);
          this.form.get('documentType')?.disable();
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? '::FailedToLoad'),
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.isEditMode) {
      const dto = this.form.getRawValue() as unknown as UpdateApprovalRuleDto;
      this.service.updateRule(this.ruleId!, dto).subscribe({
        next: () => {
          this.form.markAsPristine();
          this.toaster.success('::SuccessfullyUpdated');
          this.router.navigate(['/workflow/rules']);
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? '::FailedToUpdate'),
      });
      return;
    }

    const dto = this.form.getRawValue() as unknown as CreateApprovalRuleDto;
    this.service.createRule(dto).subscribe({
      next: () => {
        this.form.markAsPristine();
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/workflow/rules']);
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? '::FailedToCreate'),
    });
  }

  cancel(): void {
    this.router.navigate(['/workflow/rules']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
