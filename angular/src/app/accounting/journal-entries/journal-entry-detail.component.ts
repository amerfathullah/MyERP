import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import { JournalEntryService } from '../../proxy/accounting/journal-entry.service';

@Component({
  selector: 'app-journal-entry-detail',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, DocumentWorkflowComponent, ActivityLogComponent, VoucherLedgerComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="je()?.entryNumber || ('JournalEntry' | abpLocalization)">
      @if (!je()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <app-document-workflow [actions]="workflowActions" (actionClicked)="onAction($event)" />

        <!-- Summary Cards -->
        <div class="row mb-4">
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'TotalDebit' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-success">{{ totalDebit() | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'TotalCredit' | abpLocalization }}</div>
                <div class="fs-3 fw-bold text-danger">{{ totalCredit() | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'Difference' | abpLocalization }}</div>
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
                  <tr><td class="text-muted" style="width:40%">{{ 'EntryNumber' | abpLocalization }}</td><td class="fw-bold">{{ je()!.entryNumber }}</td></tr>
                  <tr><td class="text-muted">{{ 'Status' | abpLocalization }}</td><td><app-status-badge [status]="je()!.status" /></td></tr>
                  <tr><td class="text-muted">{{ 'PostingDate' | abpLocalization }}</td><td>{{ je()!.postingDate | date:'dd/MM/yyyy' }}</td></tr>
                </table>
              </div>
              <div class="col-md-6">
                <table class="table table-borderless table-sm mb-0">
                  <tr><td class="text-muted" style="width:40%">{{ 'ReferenceType' | abpLocalization }}</td><td>{{ je()!.referenceType || '-' }}</td></tr>
                  <tr><td class="text-muted">{{ 'Notes' | abpLocalization }}</td><td>{{ je()!.notes || '-' }}</td></tr>
                  <tr><td class="text-muted">{{ 'CreatedDate' | abpLocalization }}</td><td>{{ je()!.creationTime | date:'dd/MM/yyyy HH:mm' }}</td></tr>
                </table>
              </div>
            </div>
          </div>
        </div>

        <!-- GL Lines Table -->
        <div class="card mb-4">
          <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-list me-2"></i>{{ 'JournalEntryLines' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ 'Account' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Debit' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Credit' | abpLocalization }}</th>
                  <th>{{ 'Description' | abpLocalization }}</th>
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
                    <td class="text-end font-monospace">{{ line.isDebit ? (line.amount | number:'1.2-2') : '' }}</td>
                    <td class="text-end font-monospace">{{ !line.isDebit ? (line.amount | number:'1.2-2') : '' }}</td>
                    <td class="text-muted">{{ line.description || '-' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-light fw-bold">
                <tr>
                  <td colspan="2" class="text-end">{{ 'Total' | abpLocalization }}</td>
                  <td class="text-end font-monospace">{{ totalDebit() | number:'1.2-2' }}</td>
                  <td class="text-end font-monospace">{{ totalCredit() | number:'1.2-2' }}</td>
                  <td></td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        <app-activity-log documentType="JournalEntry" [documentId]="je()!.id" />

        <!-- Ledger View (JE IS the GL entry, show for Posted entries) -->
        @if (je()!.status === 'Posted') {
          <app-voucher-ledger
            [voucherType]="'JournalEntry'"
            [voucherId]="je()!.id!"
            [companyId]="je()!.companyId!" />
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

  je = signal<any>(null);
  totalDebit = signal(0);
  totalCredit = signal(0);
  difference = signal(0);

  get workflowActions(): WorkflowAction[] {
    const j = this.je();
    if (!j) return [];
    const actions: WorkflowAction[] = [];
    if (j.status === 'Draft') {
      actions.push({ name: 'post', label: 'Post', icon: 'check-double', color: 'success' });
    }
    if (j.status === 'Posted') {
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadEntry(id);
  }

  onAction(action: string): void {
    const id = this.je()!.id;
    switch (action) {
      case 'post':
        this.journalEntryService.post(id).subscribe({
          next: () => { this.toaster.success('Journal entry posted.'); this.reload(); },
          error: () => {},
        });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(s => {
          if (s === Confirmation.Status.confirm) {
            this.journalEntryService.cancel(id).subscribe({
              next: () => this.reload(),
              error: () => {},
            });
          }
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
    setTimeout(() => {
      const id = this.route.snapshot.paramMap.get('id')!;
      this.loadEntry(id);
    }, 500);
  }
}
