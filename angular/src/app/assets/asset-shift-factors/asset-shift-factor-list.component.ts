import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { AssetShiftFactorService } from '../../proxy/assets/asset-shift-factor.service';
import type { AssetShiftFactorDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-shift-factor-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'AssetShiftFactors' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetShiftFactors' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/shift-factors/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewAssetShiftFactor' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-clock fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoAssetShiftFactorsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'ShiftName' | abpLocalization }}</th>
                <th>{{ 'Factor' | abpLocalization }}</th>
                <th>{{ 'Default' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr>
                    <td class="fw-semibold">{{ item.shiftName }}</td>
                    <td>{{ item.factor }}</td>
                    <td>
                      @if (item.isDefault) {
                        <span class="badge bg-success">{{ 'Yes' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-light text-dark">{{ 'No' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/assets/shift-factors', item.id, 'edit']">
                          <i class="fa fa-edit"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="delete(item)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetShiftFactorListComponent implements OnInit {
  private service = inject(AssetShiftFactorService);
  private confirmation = inject(ConfirmationService);

  items = signal<AssetShiftFactorDto[]>([]);

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.service.getList({ maxResultCount: 200 } as any).subscribe({
      next: r => this.items.set(r.items ?? []),
    });
  }

  delete(item: AssetShiftFactorDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(item.id!).subscribe({ next: () => this.loadData() });
    });
  }
}
