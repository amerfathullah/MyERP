import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityInspectionDetailService, type QualityInspectionDetailDto } from '../../proxy/detail-services';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-qi-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'QualityInspections' | abpLocalization">
  <app-breadcrumb />
      @if (d) {
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-3"><strong>{{ 'Item' | abpLocalization }}:</strong> {{ d.itemName }}</div>
            <div class="col-md-3"><strong>{{ 'InspectionType' | abpLocalization }}:</strong> {{ ['Incoming','Outgoing','In Process'][d.inspectionType ?? 0] }}</div>
            <div class="col-md-3"><strong>{{ 'InspectionDate' | abpLocalization }}:</strong> {{ d.inspectionDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-3">
              <span class="badge" [ngClass]="{'bg-success': d.status===1, 'bg-danger': d.status===2, 'bg-secondary': d.status===0}">{{ ['Draft','Accepted','Rejected'][d.status ?? 0] }}</span>
            </div>
          </div>
        </div></div>
        <div class="card"><div class="card-body">
          <h6>{{ 'Readings' | abpLocalization }}</h6>
          <table class="table table-sm">
            <thead><tr><th>{{ 'Specification' | abpLocalization }}</th><th>{{ 'Expected' | abpLocalization }}</th><th>{{ 'Reading' | abpLocalization }}</th><th>{{ 'Result' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (r of d.readings ?? []; track r.id) {
                <tr>
                  <td>{{ r.specification }}</td>
                  <td>{{ r.isNumeric ? (r.minValue + ' – ' + r.maxValue) : r.expectedValue }}</td>
                  <td>{{ r.readingValue }}</td>
                  <td><span class="badge" [ngClass]="{'bg-success': r.status===1, 'bg-danger': r.status===2}">{{ ['—','Pass','Fail'][r.status ?? 0] }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `,
})
export class QualityInspectionDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(QualityInspectionDetailService);
  d: QualityInspectionDetailDto | null = null;
  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
  }
}
