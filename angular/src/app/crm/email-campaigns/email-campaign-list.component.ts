import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { EmailCampaignService } from '../../proxy/crm/email-campaign.service';
import type { EmailCampaignDto } from '../../proxy/crm/models';

const STATUS_LABELS = ['Scheduled', 'In Progress', 'Completed', 'Unsubscribed'];

@Component({
  selector: 'app-email-campaign-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::EmailCampaigns' | abpLocalization">
      <div class="d-flex justify-content-end mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/crm/email-campaigns/new">
          <i class="fa fa-plus me-1"></i>{{ '::New' | abpLocalization }}
        </button>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle">
          <thead>
            <tr>
              <th>{{ '::Recipient' | abpLocalization }}</th>
              <th>{{ '::StartDate' | abpLocalization }}</th>
              <th>{{ '::EndDate' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td class="text-truncate" style="max-width:220px">{{ item.recipientId }}</td>
                <td>{{ item.startDate | date:'dd/MM/yyyy' }}</td>
                <td>{{ item.endDate | date:'dd/MM/yyyy' }}</td>
                <td><span class="badge bg-info">{{ statusLabel(item.status) }}</span></td>
                <td>
                  @if (item.status !== 3 && item.status !== 2) {
                    <button class="btn btn-sm btn-outline-danger" (click)="unsubscribe(item)"><i class="fa fa-ban me-1"></i>{{ '::Unsubscribe' | abpLocalization }}</button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </abp-page>
  `,
})
export class EmailCampaignListComponent implements OnInit {
  private service = inject(EmailCampaignService);
  private toaster = inject(ToasterService);

  items = signal<EmailCampaignDto[]>([]);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.service.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe({ next: (r) => this.items.set(r.items ?? []), error: () => {} });
  }

  statusLabel(status: number | undefined): string { return STATUS_LABELS[status ?? 0] ?? 'Scheduled'; }

  unsubscribe(item: EmailCampaignDto): void {
    this.service.unsubscribe(item.id!).subscribe({
      next: () => { this.toaster.success('::SuccessfullyUpdated'); this.load(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
