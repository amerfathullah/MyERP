import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CashierClosingService } from '../../proxy/accounting/cashier-closing.service';
import type { CashierClosingDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-cashier-closing-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">{{ '::CashierClosings' | abpLocalization }}</h5>
        <a routerLink="/accounting/cashier-closings/new" class="btn btn-primary btn-sm">
          <i class="fa fa-plus me-1"></i>{{ '::NewCashierClosing' | abpLocalization }}
        </a>
      </div>
      <div class="card-body">
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" [placeholder]="'::Placeholder:SearchByNumberOrUser' | abpLocalization"
              [(ngModel)]="filterText" (ngModelChange)="loadList()">
          </div>
          <div class="col-md-3">
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (ngModelChange)="loadList()">
          </div>
          <div class="col-md-3">
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (ngModelChange)="loadList()">
          </div>
          <div class="col-md-2">
            <button class="btn btn-outline-secondary btn-sm w-100" (click)="resetFilters()">{{ '::Reset' | abpLocalization }}</button>
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-hover table-bordered table-sm align-middle">
            <thead class="table-light">
              <tr>
                <th>{{ '::ClosingNumber' | abpLocalization }}</th>
                <th>{{ '::User' | abpLocalization }}</th>
                <th>{{ '::Date' | abpLocalization }}</th>
                <th>{{ '::ShiftTime' | abpLocalization }}</th>
                <th class="text-end">{{ '::Custody' | abpLocalization }}</th>
                <th class="text-end">{{ '::Expense' | abpLocalization }}</th>
                <th class="text-end">{{ '::Outstanding' | abpLocalization }}</th>
                <th class="text-end">{{ '::NetAmount' | abpLocalization }}</th>
                <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                <th class="text-center">{{ '::Action' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @for (item of items; track item.id) {
                <tr>
                  <td class="fw-bold">
                    <a [routerLink]="['/accounting/cashier-closings', item.id]">{{ item.closingNumber }}</a>
                  </td>
                  <td>{{ item.userName }}</td>
                  <td>{{ item.date | date:'yyyy-MM-dd' }}</td>
                  <td>{{ item.fromTime }} - {{ item.toTime }}</td>
                  <td class="text-end">{{ item.custody | number:'1.2-2' }}</td>
                  <td class="text-end">{{ item.expense | number:'1.2-2' }}</td>
                  <td class="text-end">{{ item.outstandingAmount | number:'1.2-2' }}</td>
                  <td class="text-end fw-bold">{{ item.netAmount | number:'1.2-2' }}</td>
                  <td class="text-center">
                    <span class="badge" [ngClass]="item.isSubmitted ? 'bg-success' : 'bg-warning text-dark'">
                      {{ (item.isSubmitted ? '::Submitted' : '::Draft') | abpLocalization }}
                    </span>
                  </td>
                  <td class="text-center">
                    <a [routerLink]="['/accounting/cashier-closings', item.id]" class="btn btn-outline-primary btn-sm p-1">
                      <i class="fa fa-eye"></i>
                    </a>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="10" class="text-center py-4 text-muted">{{ '::NoCashierClosingsFound' | abpLocalization }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class CashierClosingListComponent implements OnInit {
  private service = inject(CashierClosingService);

  items: CashierClosingDto[] = [];
  filterText = '';
  fromDate: string | null = null;
  toDate: string | null = null;

  ngOnInit() {
    this.loadList();
  }

  loadList() {
    this.service.getList({
      filter: this.filterText,
      fromDate: this.fromDate,
      toDate: this.toDate,
      skipCount: 0,
      maxResultCount: 50,
    }).subscribe(res => {
      this.items = res.items || [];
    });
  }

  resetFilters() {
    this.filterText = '';
    this.fromDate = null;
    this.toDate = null;
    this.loadList();
  }
}
