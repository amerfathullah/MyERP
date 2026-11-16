import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import { DocumentConnectionsComponent } from '../../shared/components/document-connections/document-connections.component';
import { JournalEntryService } from '../../proxy/accounting/journal-entry.service';
import type { JournalEntryDto } from '../../proxy/accounting/models';

const VOUCHER_TYPE_LABELS: Record<number, string> = {
  0: 'Journal Entry', 1: 'Inter Company Journal Entry', 2: 'Bank Entry', 3: 'Cash Entry',
  4: 'Credit Card Entry', 5: 'Debit Note', 6: 'Credit Note', 7: 'Contra Entry',
  8: 'Excise Entry', 9: 'Write Off Entry', 10: 'Opening Entry', 11: 'Depreciation Entry',
  12: 'Exchange Rate Revaluation', 13: 'Exchange Gain Or Loss', 14: 'Deferred Revenue',
  15: 'Deferred Expense', 16: 'Reversal', 17: 'Period Closing',
};

@Component({
  selector: 'app-journal-entry-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, DocumentWorkflowComponent, ActivityLogComponent, VoucherLedgerComponent, DocumentConnectionsComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <app-breadcrumb />
    <abp-page [title]="je()?.entryNumber || ('::JournalEntry' | abpLocalization)">
      @if (!je()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <app-document-workflow [actions]="workflowActions" (actionClicked)="onAction($event)" />

        <!-- Summary Cards -->
        <div class="row mb-4">
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ '::TotalDebit' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-success">{{ totalDebit() | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ '::TotalCredit' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-danger">{{ totalCredit() | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ '::Difference' | abpLocalization }}</div>
                <div class="fs-3 fw-bold" [class.text-success]="difference() === 0" [class.text-danger]="difference() !== 0">
                  {{ difference() | number:'1.2-2' }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Entry Info -->
        <div class="card mb-4">
          <div class="card-body">
            <div class="row">
              <div class="col-md-6">
                <table class="table table-borderless table-sm mb-0">
                  <tr><td class="text-muted" style="width:40%">{{ '::EntryNumber' | abpLocalization }}</td><td class="fw-bold">{{ je()!.entryNumber }}</td></tr>
                  <tr><td class="text-muted">{{ '::EntryType' | abpLocalization }}</td><td>{{ voucherTypeLabel(je()!.voucherType) }}</td></tr>
                  <tr><td class="text-muted">{{ '::Status' | abpLocalization }}</td><td><app-status-badge [status]="je()!.status!" /></td></tr>
                  <tr><td class="text-muted">{{ '::PostingDate' | abpLocalization }}</td><td>{{ je()!.postingDate | date:'dd/MM/yyyy' }}</td></tr>
                </table>
              </div>
              <div class="col-md-6">
                <table class="table table-borderless table-sm mb-0">
                  <tr><td class="text-muted" style="width:40%">{{ '::ReferenceType' | abpLocalization }}</td><td>{{ je()!.referenceType || '-' }}</td></tr>
                  <tr><td class="text-muted">{{ '::Notes' | abpLocalization }}</td><td>{{ je()!.narration || '-' }}</td></tr>
                  @if (je()!.reversalOfId) {
                    <tr>
                      <td class="text-muted">{{ '::ReversalOf' | abpLocalization }}</td>
                      <td>
                        <a [routerLink]="['/accounting/journal-entries', je()!.reversalOfId]" class="text-decoration-none fw-semibold">
                          {{ je()!.reversalOfEntryNumber || je()!.reversalOfId }}
                        </a>
                      </td>
                    </tr>
                  }
                  @if (je()!.isMultiCurrency) {
                    <tr><td class="text-muted">{{ '::MultiCurrency' | abpLocalization }}</td><td><i class="fa fa-check text-success"></i></td></tr>
                  }
                </table>
              </div>
            </div>
          </div>
        </div>

        <!-- GL Lines Table -->
        <div class="card mb-4">
          <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-list me-2"></i>{{ '::JournalEntryLines' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ '::Account' | abpLocalization }}</th>
                  @if (je()!.isMultiCurrency) {
                    <th>{{ '::Currency' | abpLocalization }}</th>
                    <th class="text-end">{{ '::AmountInAccountCurrency' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ExchangeRate' | abpLocalization }}</th>
                  }
                  <th class="text-end">{{ '::Debit' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Credit' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (line of je()!.lines; track $index; let i = $index) {
                  <tr>
                    <td>{{ i + 1 }}</td>
                    <td>
                      @if (line.accountCode) {
                        <span class="text-muted small">{{ line.accountCode }}</span>
                        {{ line.accountName }}
                      } @else {
                        {{ line.accountId }}
                      }
                    </td>
                    @if (je()!.isMultiCurrency) {
                      <td>{{ line.accountCurrency }}</td>
                      <td class="text-end font-monospace">{{ line.amountInAccountCurrency | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ line.exchangeRate | number:'1.0-6' }}</td>
                    }
                    <td class="text-end font-monospace">{{ line.isDebit ? (line.amount | number:'1.2-2') : '' }}</td>
                    <td class="text-end font-monospace">{{ !line.isDebit ? (line.amount | number:'1.2-2') : '' }}</td>
                    <td class="text-muted">{{ line.description || '-' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-light fw-bold">
                <tr>
                  <td [attr.colspan]="je()!.isMultiCurrency ? 5 : 2" class="text-end">{{ '::Total' | abpLocalization }}</td>
                  <td class="text-end font-monospace">{{ totalDebit() | number:'1.2-2' }}</td>
                  <td class="text-end font-monospace">{{ totalCredit() | number:'1.2-2' }}</td>
                  <td></td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        <app-document-connections [documentType]="'JournalEntry'" [documentId]="je()!.id!" />

        <app-activity-log documentType="JournalEntry" [documentId]="je()!.id!" />

        <!-- Ledger View (JE IS the GL entry, show for Posted entries) -->
        @if (je()!.status === 'Posted') {
          <app-voucher-ledger
            [voucherType]="'JournalEntry'"
            [voucherId]="je()!.id!" />
        }
      }
    </abp-page>
  `
})
export class JournalEntryDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private journalEntryService = inject(JournalEntryService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService, { optional: true });
  private permissionService = inject(PermissionService, { optional: true });

  je = signal<JournalEntryDto | null>(null);
  totalDebit = signal(0);
  totalCredit = signal(0);
  difference = signal(0);

  voucherTypeLabel(type: number | undefined): string {
    return VOUCHER_TYPE_LABELS[type ?? 0] ?? 'Journal Entry';
  }

  private label(key: string, fallback: string): string {
    return this.l?.instant(key) || fallback;
  }

  get workflowActions(): WorkflowAction[] {
    const j = this.je();
    if (!j) return [];
    const actions: WorkflowAction[] = [];
    const canPost = this.permissionService?.getGrantedPolicy('MyERP.JournalEntries.Post') ?? true;
    const canCreate = this.permissionService?.getGrantedPolicy('MyERP.JournalEntries.Create') ?? true;

    if (j.status === 'Draft' && canPost) {
      actions.push({ name: 'post', label: this.label('::Post', 'Post'), icon: 'check-double', color: 'success' });
    }
    if (j.status === 'Posted') {
      if (canPost) {
        actions.push({ name: 'cancel', label: this.label('::Cancel', 'Cancel'), icon: 'ban', color: 'danger' });
      }
      // Per ERPNext PR #58092: disallow reversing an entry that is already a reversal or has an active reversal.
      const isReversible = j.voucherType !== 16 && !j.reversalOfId && !j.hasActiveReversal;
      if (isReversible && canPost) {
        actions.push({ name: 'reverse', label: this.label('::CreateReversal', 'CreateReversal'), icon: 'rotate-left', color: 'warning' });
      }
      if (isReversible && canCreate) {
        actions.push({ name: 'reverse_draft', label: this.label('::MakeReverseDraft', 'Reverse as Draft'), icon: 'copy', color: 'info' });
      }
    }
    if (j.status === 'Cancelled' && canCreate) {
      actions.push({ name: 'amend', label: this.label('::Amend', 'Amend'), icon: 'file-circle-plus', color: 'success' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadEntry(id);
  }

  private handleError(err: any): void {
    const reason = err?.error?.error?.data?.detail || err?.error?.error?.message;
    this.toaster.error(reason || '::OperationFailed');
  }

  onAction(action: string): void {
    const id = this.je()!.id!;
    switch (action) {
      case 'post':
        this.journalEntryService.post(id).subscribe({
          next: () => { this.toaster.success('::SuccessfullyPosted'); this.reload(); },
          error: (err: any) => this.handleError(err),
        });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(s => {
          if (s === Confirmation.Status.confirm) {
            this.journalEntryService.cancel(id).subscribe({
              next: () => this.reload(),
              error: (err: any) => this.handleError(err),
            });
          }
        });
        break;
      case 'amend':
        this.journalEntryService.amend(id).subscribe({
          next: (amended: any) => this.router.navigate(['/accounting/journal-entries', amended.id]),
          error: (err: any) => this.handleError(err),
        });
        break;
      case 'reverse':
        this.confirmation.warn('::CreateReversalConfirmation', '::AreYouSure').subscribe(s => {
          if (s === Confirmation.Status.confirm) {
            this.journalEntryService.createReversal(id).subscribe({
              next: (reversal: any) => {
                this.toaster.success('::SuccessfullyCreated');
                this.router.navigate(['/accounting/journal-entries', reversal.id]);
              },
              error: (err: any) => this.handleError(err),
            });
          }
        });
        break;
      case 'reverse_draft':
        this.journalEntryService.createReverseJournalEntry(id).subscribe({
          next: (reversal: any) => {
            this.toaster.success('::SuccessfullyCreated');
            this.router.navigate(['/accounting/journal-entries', reversal.id, 'edit']);
          },
          error: (err: any) => this.handleError(err),
        });
        break;
    }
  }

  private loadEntry(id: string): void {
    this.journalEntryService.get(id).subscribe(data => {
      this.je.set(data);
      const lines = (data as any).lines || [];
      const dr = lines.filter((l: any) => l.isDebit).reduce((s: number, l: any) => s + l.amount, 0);
      const cr = lines.filter((l: any) => !l.isDebit).reduce((s: number, l: any) => s + l.amount, 0);
      this.totalDebit.set(dr);
      this.totalCredit.set(cr);
      this.difference.set(Math.round((dr - cr) * 100) / 100);
    });
  }

  private reload(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadEntry(id);
  }
}
