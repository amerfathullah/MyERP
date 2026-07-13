import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { HolidayListService, type HolidayListDto } from '../../proxy/human-resources/hr-config.service';

@Component({
  selector: 'app-holiday-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'HolidayLists' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/hr/holiday-lists/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewHolidayList' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <app-loading-overlay /> }

      @if (!isLoading && lists.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-calendar-days fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoHolidayListsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'Year' | abpLocalization }}</th>
              <th>{{ 'WeeklyOff' | abpLocalization }}</th>
              <th>{{ 'Holidays' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (hl of lists; track hl.id) {
                <tr>
                  <td>{{ hl.name }}</td>
                  <td>{{ hl.year }}</td>
                  <td>{{ hl.weeklyOff ?? '—' }}</td>
                  <td>{{ (hl.holidays ?? []).length }}</td>
                  <td>
                    <a class="btn btn-sm btn-outline-primary" [routerLink]="['/hr/holiday-lists', hl.id]">
                      <i class="fa fa-eye"></i>
                    </a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `,
})
export class HolidayListListComponent implements OnInit {
  private service = inject(HolidayListService);
  lists: HolidayListDto[] = [];
  isLoading = false;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 50 }).subscribe({
      next: (r) => { this.lists = r.items ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
  }
}
