import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { StockClosingService } from '../../proxy/inventory/stock-closing.service';
import type { StockClosingEntryDto } from '../../proxy/inventory/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';

const STATUS_LABELS = ['Draft', 'Submitted', 'Cancelled'] as const;

@Component({
  selector: 'app-stock-closing-detail',
  standalone: true,
  imports: [
    CommonModule, RouterModule, PageModule, LocalizationPipe,
    StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent, VoucherLedgerComponent,
  ],
  templateUrl: './stock-closing-detail.component.html',
  styleUrls: ['./stock-closing-detail.component.scss'],
})
export class StockClosingDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private service = inject(StockClosingService);
  private toaster = inject(ToasterService);

  entry: StockClosingEntryDto | null = null;
  loading = signal(false);
  actionLoading = signal(false);

  get statusLabel(): string {
    return STATUS_LABELS[this.entry?.status ?? 0] ?? 'Draft';
  }

  get isDraft(): boolean {
    return (this.entry?.status ?? 0) === 0;
  }

  get isSubmitted(): boolean {
    return (this.entry?.status ?? 0) === 1;
  }

  get hasBalances(): boolean {
    return (this.entry?.totalEntries ?? 0) > 0;
  }

  ngOnInit(): void {
    this.loadEntry();
  }

  generate(): void {
    if (!this.entry) return;
    this.actionLoading.set(true);
    this.service.generate({
      companyId: this.entry.companyId,
      toDate: this.entry.toDate,
    } as any).subscribe({
      next: (result) => {
        this.toaster.success('::SuccessfullyGenerated');
        this.entry = result;
        this.actionLoading.set(false);
        // Reload to get balances
        this.loadEntry();
      },
      error: () => this.actionLoading.set(false),
    });
  }

  submit(): void {
    if (!this.entry) return;
    this.confirmation.warn('::StockClosingSubmitConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.actionLoading.set(true);
      this.service.submit(this.entry!.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullySubmitted');
          this.loadEntry();
        },
        error: () => this.actionLoading.set(false),
      });
    });
  }

  cancel(): void {
    if (!this.entry) return;
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.actionLoading.set(true);
      this.service.cancel(this.entry!.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyCancelled');
          this.loadEntry();
        },
        error: () => this.actionLoading.set(false),
      });
    });
  }

  private loadEntry(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loading.set(true);
    this.service.get(id).subscribe({
      next: (result) => {
        this.entry = result;
        this.loading.set(false);
        this.actionLoading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
