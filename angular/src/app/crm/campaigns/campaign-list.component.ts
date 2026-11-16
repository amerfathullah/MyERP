import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { CampaignService } from '../../proxy/crm/campaign.service';
import type { CampaignDto } from '../../proxy/crm/models';

@Component({
  selector: 'app-campaign-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::Campaigns' | abpLocalization">
      <div class="d-flex justify-content-between gap-2 mb-3">
        <input type="text" class="form-control form-control-sm" style="width:200px"
          [(ngModel)]="searchTerm" (keyup.enter)="load()" [placeholder]="'::Placeholder:Search' | abpLocalization">
        <button class="btn btn-primary btn-sm" routerLink="/crm/campaigns/new">
          <i class="fa fa-plus me-1"></i>{{ '::New' | abpLocalization }}
        </button>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle">
          <thead><tr><th>{{ '::CampaignName' | abpLocalization }}</th><th>{{ '::UtmCampaign' | abpLocalization }}</th><th>{{ '::Description' | abpLocalization }}</th><th>{{ '::EmailSchedules' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td>{{ item.campaignName }}</td>
                <td>
                  @if (item.id && utmSlugs()[item.id]) {
                    <span class="badge bg-info text-dark font-monospace">{{ utmSlugs()[item.id] }}</span>
                  } @else if (item.id) {
                    <button type="button" class="btn btn-sm btn-outline-secondary" (click)="loadUtmSlug(item.id!)">
                      <i class="fa fa-tag me-1"></i>{{ '::UtmCampaign' | abpLocalization }}
                    </button>
                  }
                </td>
                <td>{{ item.description ?? '—' }}</td>
                <td>{{ item.emailSchedules?.length ?? 0 }}</td>
                <td>
                  <div class="btn-group btn-group-sm">
                    <a class="btn btn-outline-primary" [routerLink]="'/crm/campaigns/' + item.id"><i class="fa fa-edit"></i></a>
                    <button class="btn btn-outline-danger" (click)="delete(item)"><i class="fa fa-trash"></i></button>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </abp-page>
  `,
})
export class CampaignListComponent implements OnInit {
  private service = inject(CampaignService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<CampaignDto[]>([]);
  utmSlugs = signal<Record<string, string>>({});
  searchTerm = '';

  ngOnInit(): void { this.load(); }

  loadUtmSlug(id: string): void {
    this.service.getUtmCampaign(id).subscribe({
      next: slug => this.utmSlugs.update(map => ({ ...map, [id]: slug ?? '' })),
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }

  load(): void {
    this.service.getList({ skipCount: 0, maxResultCount: 200, sorting: 'campaignName', filter: this.searchTerm } as any)
      .subscribe({ next: (r) => this.items.set(r.items ?? []), error: () => {} });
  }

  delete(item: CampaignDto): void {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === 'confirm') {
        this.service.delete(item.id!).subscribe({
          next: () => { this.toaster.success('::SuccessfullyDeleted'); this.load(); },
          error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Delete failed'),
        });
      }
    });
  }
}
