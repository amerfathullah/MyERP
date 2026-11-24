import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { TermsAndConditionsService } from '../../proxy/core/terms-and-conditions.service';
import { TermsAndConditionsDto } from '../../proxy/core/models';

@Component({
  selector: 'app-terms-and-conditions-list',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::TermsAndConditions' | abpLocalization }}</h5>
        <a routerLink="/settings/terms-and-conditions/new" class="btn btn-primary btn-sm">{{ '::NewTermsAndConditions' | abpLocalization }}</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered table-hover">
          <thead class="table-light">
            <tr>
              <th>{{ '::Title' | abpLocalization }}</th>
              <th>{{ '::Selling' | abpLocalization }}</th>
              <th>{{ '::Buying' | abpLocalization }}</th>
              <th>{{ '::Status' | abpLocalization }}</th>
              <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track item.id) {
              <tr>
                <td>
                  <a [routerLink]="['/settings/terms-and-conditions', item.id, 'edit']" class="fw-semibold">
                    {{ item.title }}
                  </a>
                </td>
                <td>
                  <span class="badge" [ngClass]="item.isSelling ? 'bg-success' : 'bg-secondary'">
                    {{ (item.isSelling ? '::Yes' : '::No') | abpLocalization }}
                  </span>
                </td>
                <td>
                  <span class="badge" [ngClass]="item.isBuying ? 'bg-info' : 'bg-secondary'">
                    {{ (item.isBuying ? '::Yes' : '::No') | abpLocalization }}
                  </span>
                </td>
                <td>
                  @if (item.isDisabled) {
                    <span class="badge bg-warning text-dark">{{ '::Disabled' | abpLocalization }}</span>
                  } @else {
                    <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                  }
                </td>
                <td class="text-end">
                  <a [routerLink]="['/settings/terms-and-conditions', item.id, 'edit']" class="btn btn-sm btn-outline-primary me-2">{{ '::Edit' | abpLocalization }}</a>
                  <button class="btn btn-sm btn-outline-danger" (click)="delete(item)">{{ '::Delete' | abpLocalization }}</button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="text-center text-muted py-4">{{ '::NoTermsAndConditionsDefined' | abpLocalization }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class TermsAndConditionsListComponent implements OnInit {
  private service = inject(TermsAndConditionsService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items: TermsAndConditionsDto[] = [];

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe(res => {
      this.items = res.items ?? [];
    });
  }

  delete(item: TermsAndConditionsDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(item.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          this.load();
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::Failed'),
      });
    });
  }
}
