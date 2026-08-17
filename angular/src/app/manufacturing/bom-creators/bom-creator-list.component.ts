import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { BomCreatorService } from '../../proxy/manufacturing/bom-creator.service';
import type { BomCreatorDto } from '../../proxy/manufacturing/models';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-bom-creator-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent],
  template: `
    <abp-page [title]="'BomCreators' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/manufacturing/bom-creators/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewBomCreator' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) {
        <app-loading-overlay />
      }

      @if (!isLoading && creators.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-sitemap fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoBomCreatorsYet' | abpLocalization }}</p>
          <button class="btn btn-primary" routerLink="/manufacturing/bom-creators/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewBomCreator' | abpLocalization }}
          </button>
        </div>
      } @else if (!isLoading) {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'FinishedGood' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                  <th>{{ 'Items' | abpLocalization }}</th>
                  <th class="text-end">{{ 'TotalCost' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (c of creators; track c.id) {
                  <tr>
                    <td>{{ itemName(c.finishedGoodItemId) }}</td>
                    <td class="text-end">{{ c.qty }}</td>
                    <td>{{ (c.items ?? []).length }}</td>
                    <td class="text-end">{{ c.rawMaterialCost | number:'1.2-2' }}</td>
                    <td>{{ statusLabel(c.status) }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/manufacturing/bom-creators', c.id]">
                          <i class="fa fa-pen"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="remove(c)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <app-pagination
        [totalCount]="totalCount"
        [pageSize]="pageSize"
        [currentPage]="currentPage"
        (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class BomCreatorListComponent implements OnInit {
  private service = inject(BomCreatorService);
  private itemService = inject(ItemService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  creators: BomCreatorDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  private items = new Map<string, string>();

  ngOnInit(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      (r.items ?? []).forEach((i: any) => this.items.set(i.id, `${i.itemCode} — ${i.itemName}`)));
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.creators = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  itemName(id: string | undefined): string { return (id && this.items.get(id)) ?? '—'; }
  statusLabel(status: number | undefined): string {
    return ['Draft', 'Completed', 'Failed'][status ?? 0] ?? 'Draft';
  }

  remove(c: BomCreatorDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(c.id!).subscribe(() => {
        this.toaster.success('::SuccessfullyDeleted');
        this.load();
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
