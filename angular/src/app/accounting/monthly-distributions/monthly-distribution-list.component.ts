import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { MonthlyDistributionService } from '../../proxy/accounting/monthly-distribution.service';
import type { MonthlyDistributionDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-monthly-distribution-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'MonthlyDistributions' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/accounting/monthly-distributions/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewMonthlyDistribution' | abpLocalization }}
        </button>
      </div>

      @if (distributions.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-bar-chart fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoMonthlyDistributionsYet' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'DistributionName' | abpLocalization }}</th>
                  <th class="text-end">{{ 'TotalAllocated' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (d of distributions; track d.id) {
                  <tr>
                    <td>{{ d.distributionName }}</td>
                    <td class="text-end">{{ total(d) | number:'1.0-2' }}%</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/accounting/monthly-distributions', d.id]">
                          <i class="fa fa-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="remove(d)"><i class="fa fa-trash"></i></button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class MonthlyDistributionListComponent implements OnInit {
  private service = inject(MonthlyDistributionService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  distributions: MonthlyDistributionDto[] = [];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.service.getList().subscribe(r => this.distributions = r.items ?? []);
  }

  total(d: MonthlyDistributionDto): number {
    return (d.percentages ?? []).reduce((s, p) => s + (p.percentageAllocation ?? 0), 0);
  }

  remove(d: MonthlyDistributionDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(d.id!).subscribe(() => { this.toaster.success('::SuccessfullyDeleted'); this.load(); });
    });
  }
}
