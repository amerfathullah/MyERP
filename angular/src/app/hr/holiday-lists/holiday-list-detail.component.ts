import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HolidayListDetailService, type HolidayListDetailDto } from '../../proxy/detail-services';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-holiday-list-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'HolidayLists' | abpLocalization">
  <app-breadcrumb />
      @if (data) {
        <div class="card mb-3"><div class="card-body">
          <h5>{{ data.name }} ({{ data.year }})</h5>
          <p><strong>{{ 'WeeklyOff' | abpLocalization }}:</strong> {{ data.weeklyOff ?? '—' }}</p>
        </div></div>
        <div class="card"><div class="card-body">
          <table class="table table-sm">
            <thead><tr><th>{{ 'Date' | abpLocalization }}</th><th>{{ 'Description' | abpLocalization }}</th><th>{{ 'WeeklyOff' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (h of data.holidays ?? []; track h.id) {
                <tr><td>{{ h.holidayDate | date:'dd/MM/yyyy' }}</td><td>{{ h.description }}</td><td>{{ h.isWeeklyOff ? '✓' : '' }}</td></tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `,
})
export class HolidayListDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(HolidayListDetailService);
  data: HolidayListDetailDto | null = null;
  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.data = r);
  }
}
