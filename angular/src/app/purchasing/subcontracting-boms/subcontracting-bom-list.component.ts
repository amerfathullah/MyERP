import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { SubcontractingBomService } from '../../proxy/purchasing/subcontracting-bom.service';
import type { SubcontractingBomDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-subcontracting-bom-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'SubcontractingBoms' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/purchasing/subcontracting-boms/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewSubcontractingBom' | abpLocalization }}
        </button>
      </div>

      @if (boms.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-diagram-project fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSubcontractingBomsYet' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'FinishedGood' | abpLocalization }}</th>
                  <th class="text-end">{{ 'FinishedGoodQty' | abpLocalization }}</th>
                  <th>{{ 'ServiceItem' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ServiceItemQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ConversionFactor' | abpLocalization }}</th>
                  <th>{{ 'IsActive' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (b of boms; track b.id) {
                  <tr>
                    <td>{{ b.finishedGoodName }}</td>
                    <td class="text-end">{{ b.finishedGoodQty }} {{ b.finishedGoodUom }}</td>
                    <td>{{ b.serviceItemName }}</td>
                    <td class="text-end">{{ b.serviceItemQty }} {{ b.serviceItemUom }}</td>
                    <td class="text-end">{{ b.conversionFactor | number:'1.2-6' }}</td>
                    <td>
                      @if (b.isActive) {
                        <span class="badge bg-success-subtle text-success">{{ 'Yes' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-secondary-subtle text-secondary">{{ 'No' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/purchasing/subcontracting-boms', b.id]">
                          <i class="fa fa-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="remove(b)"><i class="fa fa-trash"></i></button>
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
export class SubcontractingBomListComponent implements OnInit {
  private service = inject(SubcontractingBomService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  boms: SubcontractingBomDto[] = [];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.service.getList({ maxResultCount: 200 } as any).subscribe(r => this.boms = r.items ?? []);
  }

  remove(b: SubcontractingBomDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(b.id!).subscribe(() => { this.toaster.success('::SuccessfullyDeleted'); this.load(); });
    });
  }
}
