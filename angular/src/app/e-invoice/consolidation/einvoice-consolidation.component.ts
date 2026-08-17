import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import type { ConsolidationCandidateDto, EInvoiceConsolidationDto } from '../../proxy/einvoice/models';

@Component({
  selector: 'app-einvoice-consolidation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    PageModule,
    LocalizationPipe,
    LhdnStatusBadgeComponent,
    PaginationComponent
  ],
  templateUrl: './einvoice-consolidation.component.html',
  styleUrls: ['./einvoice-consolidation.component.scss']
})
export class EinvoiceConsolidationComponent implements OnInit {
  private eInvoiceService = inject(EInvoiceService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  companyContext = inject(CompanyContextService);

  activeTab = signal<'candidates' | 'history'>('candidates');

  // Filter state
  fromDate = signal<string>('');
  toDate = signal<string>('');
  maxAmount = signal<number>(10000);

  // Candidates state
  candidates = signal<ConsolidationCandidateDto[]>([]);
  selectedCandidateIds = signal<string[]>([]);
  loadingCandidates = signal<boolean>(false);
  consolidating = signal<boolean>(false);

  // History state
  consolidations = signal<EInvoiceConsolidationDto[]>([]);
  totalConsolidations = signal<number>(0);
  historyPage = signal<number>(0);
  pageSize = 10;
  loadingHistory = signal<boolean>(false);
  selectedConsolidation = signal<EInvoiceConsolidationDto | null>(null);

  selectedTotal = computed(() => {
    const selectedIds = new Set(this.selectedCandidateIds());
    return this.candidates()
      .filter(c => !!c.invoiceId && selectedIds.has(c.invoiceId))
      .reduce((sum, c) => sum + (c.grandTotal || 0), 0);
  });

  ngOnInit(): void {
    this.companyContext.load();
    this.setDefaultDates();
    this.loadCandidates();
    this.loadHistory();
  }

  private setDefaultDates(): void {
    const now = new Date();
    const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);
    this.fromDate.set(startOfMonth.toISOString().substring(0, 10));
    this.toDate.set(now.toISOString().substring(0, 10));
  }

  setTab(tab: 'candidates' | 'history'): void {
    this.activeTab.set(tab);
    if (tab === 'candidates') {
      this.loadCandidates();
    } else {
      this.loadHistory();
    }
  }

  loadCandidates(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loadingCandidates.set(true);
    this.eInvoiceService.getConsolidationCandidates({
      companyId: companyId,
      fromDate: this.fromDate() ? this.fromDate() : undefined,
      toDate: this.toDate() ? this.toDate() : undefined,
      maxAmount: this.maxAmount()
    }).subscribe({
      next: (items) => {
        this.candidates.set(items ?? []);
        this.selectedCandidateIds.set([]);
        this.loadingCandidates.set(false);
      },
      error: () => {
        this.loadingCandidates.set(false);
        this.toaster.error('::FailedToLoad');
      }
    });
  }

  toggleSelectAll(event: Event): void {
    const isChecked = (event.target as HTMLInputElement).checked;
    if (isChecked) {
      this.selectedCandidateIds.set(
        this.candidates().map(c => c.invoiceId).filter((id): id is string => !!id)
      );
    } else {
      this.selectedCandidateIds.set([]);
    }
  }

  toggleSelectOne(id: string, event: Event): void {
    const isChecked = (event.target as HTMLInputElement).checked;
    const current = new Set(this.selectedCandidateIds());
    if (isChecked) {
      current.add(id);
    } else {
      current.delete(id);
    }
    this.selectedCandidateIds.set(Array.from(current));
  }

  isSelected(id: string): boolean {
    return this.selectedCandidateIds().includes(id);
  }

  areAllSelected(): boolean {
    return this.candidates().length > 0 && this.selectedCandidateIds().length === this.candidates().length;
  }

  consolidateSelected(): void {
    const ids = this.selectedCandidateIds();
    if (ids.length < 2) {
      this.toaster.warn('::SelectAtLeastTwoInvoicesToConsolidate');
      return;
    }

    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.confirmation.info(
      `Merge ${ids.length} B2C invoices totaling RM ${this.selectedTotal().toFixed(2)} into a consolidated e-Invoice?`,
      'Confirm B2C Consolidation'
    ).subscribe((status: Confirmation.Status) => {
      if (status !== Confirmation.Status.confirm) return;

      this.consolidating.set(true);
      this.eInvoiceService.consolidateInvoices({
        companyId: companyId,
        invoiceIds: ids
      }).subscribe({
        next: (createdIds) => {
          this.consolidating.set(false);
          this.toaster.success(`Successfully created ${createdIds.length} consolidated invoice(s).`);
          this.loadCandidates();
          this.loadHistory();
          this.setTab('history');
        },
        error: (err) => {
          this.consolidating.set(false);
          this.toaster.error(err?.error?.message || '::ConsolidationFailed');
        }
      });
    });
  }

  loadHistory(page: number = 0): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.historyPage.set(page);
    this.loadingHistory.set(true);

    this.eInvoiceService.getConsolidations({
      companyId: companyId,
      skipCount: page * this.pageSize,
      maxResultCount: this.pageSize
    }).subscribe({
      next: (res) => {
        this.consolidations.set(res.items ?? []);
        this.totalConsolidations.set(res.totalCount ?? 0);
        this.loadingHistory.set(false);
      },
      error: () => {
        this.loadingHistory.set(false);
        this.toaster.error('::FailedToLoad');
      }
    });
  }

  onHistoryPageChange(event: any): void {
    this.loadHistory(event.pageIndex);
  }

  submitConsolidatedToLhdn(consol: EInvoiceConsolidationDto): void {
    this.confirmation.info(
      `Submit consolidated invoice ${consol.consolidatedInvoiceNumber} to LHDN MyInvois?`,
      'Submit Consolidated Invoice'
    ).subscribe((status: Confirmation.Status) => {
      if (status !== Confirmation.Status.confirm) return;

      this.eInvoiceService.submitConsolidated({
        companyId: consol.companyId,
        sourceDocumentType: 'SalesInvoice',
        sourceDocumentId: consol.consolidatedInvoiceId,
        documentTypeCode: '01'
      }).subscribe({
        next: (submission) => {
          this.toaster.success(`LHDN Submission status: ${submission.status}`);
          this.loadHistory(this.historyPage());
        },
        error: (err) => {
          this.toaster.error(err?.error?.message || '::SubmissionFailed');
        }
      });
    });
  }

  viewOriginalInvoices(consol: EInvoiceConsolidationDto): void {
    this.selectedConsolidation.set(consol);
  }

  closeOriginalInvoicesModal(): void {
    this.selectedConsolidation.set(null);
  }
}
