import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PayrollService } from '../../proxy/human-resources/payroll.service';
import { AccountService } from '../../proxy/accounting/account.service';
import type { PayrollEntryDto } from '../../proxy/human-resources/models';
import { ToasterService } from '@abp/ng.theme.shared';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LocalizationPipe } from '@abp/ng.core';

@Component({
  selector: 'app-payroll-detail',
  standalone: true,
  imports: [
    BreadcrumbComponent, CommonModule, FormsModule, RouterModule, PageModule,
    DocumentWorkflowComponent, LoadingOverlayComponent, LocalizationPipe],
  templateUrl: './payroll-detail.component.html',
  styleUrls: ['./payroll-detail.component.scss'],
})
export class PayrollDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(PayrollService);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);

  entry: PayrollEntryDto | null = null;

  // Bank Entry dialog state
  showBankEntryDialog = false;
  isCreatingBankEntry = false;
  bankAccountId = '';
  paymentDate = new Date().toISOString().split('T')[0];
  paymentReference = '';
  bankAccounts = signal<any[]>([]);
  bankEntryResult: { journalEntryId: string; journalEntryNumber: string; totalAmount: number } | null = null;

  get workflowActions(): WorkflowAction[] {
    if (!this.entry) return [];
    const actions: WorkflowAction[] = [];
    if (this.entry.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (this.entry.status === 'Submitted') {
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: r => { this.entry = r; }, error: () => {} });

    // Load bank accounts for the bank entry dialog
    this.accountService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => {
        const bankAccs = (res.items ?? []).filter((a: any) =>
          a.accountSubType === 12 || a.accountSubType === 13); // BankAccount=12, CashAccount=13
        this.bankAccounts.set(bankAccs);
      });
  }

  onWorkflowAction(action: string): void {
    const id = this.entry!.id!;
    const reload = () => this.service.get(id).subscribe({ next: (r) => { this.entry = r; }, error: () => {} });
    if (action === 'submit') {
      this.service.submit(id).subscribe({ next: () => reload(), error: () => {} });
    } else if (action === 'cancel') {
      this.service.cancel(id).subscribe({ next: () => reload(), error: () => {} });
    }
  }

  createBankEntry(): void {
    if (!this.bankAccountId || !this.entry) return;
    this.isCreatingBankEntry = true;

    this.service.createBankEntry({
      payrollEntryId: this.entry.id!,
      bankAccountId: this.bankAccountId,
      referenceNumber: this.paymentReference || undefined,
      paymentDate: this.paymentDate || undefined,
    } as any).subscribe({
      next: (result: any) => {
        this.bankEntryResult = result;
        this.showBankEntryDialog = false;
        this.isCreatingBankEntry = false;
        this.toaster.success('::BankEntryCreated');
      },
      error: () => { this.isCreatingBankEntry = false; },
    });
  }
}
