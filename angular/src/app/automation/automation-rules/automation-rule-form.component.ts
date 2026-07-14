import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AutomationRuleStore } from '../store/automation-rule.store';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-automation-rule-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule],
  templateUrl: './automation-rule-form.component.html',
  styleUrls: ['./automation-rule-form.component.scss'],
})
export class AutomationRuleFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(AutomationRuleStore);

  form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    description: [''],
    trigger: [0, Validators.required],
    documentType: [''],
    conditionExpression: [''],
    action: [0, Validators.required],
    actionConfig: [''],
    isActive: [true],
    priority: [0],
  });

  triggers = [
    { value: 0, label: 'Document Submitted' },
    { value: 1, label: 'Document Approved' },
    { value: 2, label: 'Document Posted' },
    { value: 3, label: 'Document Cancelled' },
    { value: 4, label: 'Payment Received' },
    { value: 5, label: 'Stock Below Reorder' },
    { value: 6, label: 'Invoice Overdue' },
    { value: 7, label: 'E-Invoice Validated' },
    { value: 8, label: 'E-Invoice Rejected' },
    { value: 9, label: 'Approval Required' },
    { value: 100, label: 'Daily Schedule' },
    { value: 101, label: 'Weekly Schedule' },
    { value: 102, label: 'Monthly Schedule' }];

  actions = [
    { value: 0, label: 'Send Notification' },
    { value: 1, label: 'Send Email' },
    { value: 2, label: 'Submit to LHDN' },
    { value: 3, label: 'Create Approval Request' },
    { value: 4, label: 'Update Field' },
    { value: 5, label: 'Create Follow-up Task' },
    { value: 6, label: 'Post to Accounting' }];

  documentTypes = [
    'SalesInvoice', 'PurchaseInvoice', 'Quotation', 'SalesOrder',
    'DeliveryNote', 'PaymentEntry', 'JournalEntry', 'StockEntry'];

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.store.create({
      name: value.name!,
      description: value.description || undefined,
      trigger: value.trigger!,
      documentType: value.documentType || undefined,
      conditionExpression: value.conditionExpression || undefined,
      action: value.action!,
      actionConfig: value.actionConfig || undefined,
      isActive: value.isActive ?? true,
      priority: value.priority ?? 0,
    });
    this.router.navigate(['/automation']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}