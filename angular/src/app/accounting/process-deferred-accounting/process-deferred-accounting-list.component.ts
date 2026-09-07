import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProcessDeferredAccountingService } from '../../proxy/accounting/process-deferred-accounting.service';
import { DeferredAccountingType } from '../../proxy/accounting/deferred-accounting-type.enum';
import type { ProcessDeferredAccountingDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-process-deferred-accounting-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Process Deferred Accounting</h5>
        <a routerLink="/accounting/process-deferred-accounting/new" class="btn btn-primary btn-sm">
          <i class="fa fa-plus me-1"></i>New Deferred Process
        </a>
      </div>
      <div class="card-body">
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <input type="text" class="form-control form-control-sm" placeholder="Search by number..."
              [(ngModel)]="filterText" (ngModelChange)="loadList()">
          </div>
          <div class="col-md-3">
            <select class="form-select form-select-sm" [(ngModel)]="selectedType" (ngModelChange)="loadList()">
              <option [ngValue]="null">All Types</option>
              <option [ngValue]="DeferredAccountingType.Income">Income (Revenue)</option>
              <option [ngValue]="DeferredAccountingType.Expense">Expense</option>
            </select>
          </div>
          <div class="col-md-3">
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (ngModelChange)="loadList()">
          </div>
          <div class="col-md-2">
            <button class="btn btn-outline-secondary btn-sm w-100" (click)="resetFilters()">Reset</button>
          </div>
        </div>

        <div class="table-responsive">
          <table class="table table-hover table-bordered table-sm align-middle">
            <thead class="table-light">
              <tr>
                <th>Process #</th>
                <th>Company</th>
                <th>Type</th>
                <th>Posting Date</th>
                <th>Service Period</th>
                <th class="text-center">Entries</th>
                <th class="text-center">Status</th>
                <th class="text-center">Action</th>
              </tr>
            </thead>
            <tbody>
              @for (item of items; track item.id) {
                <tr>
                  <td class="fw-bold">
                    <a [routerLink]="['/accounting/process-deferred-accounting', item.id]">{{ item.processNumber }}</a>
                  </td>
                  <td>{{ item.companyName || '—' }}</td>
                  <td>
                    <span class="badge" [ngClass]="item.type === DeferredAccountingType.Income ? 'bg-info text-dark' : 'bg-primary'">
                      {{ item.type === DeferredAccountingType.Income ? 'Income' : 'Expense' }}
                    </span>
                  </td>
                  <td>{{ item.postingDate | date:'yyyy-MM-dd' }}</td>
                  <td>{{ item.startDate | date:'yyyy-MM-dd' }} ~ {{ item.endDate | date:'yyyy-MM-dd' }}</td>
                  <td class="text-center fw-bold">{{ item.entriesProcessed }}</td>
                  <td class="text-center">
                    @if (item.isCancelled) {
                      <span class="badge bg-danger">Cancelled</span>
                    } @else if (item.isSubmitted) {
                      <span class="badge bg-success">Submitted</span>
                    } @else {
                      <span class="badge bg-warning text-dark">Draft</span>
                    }
                  </td>
                  <td class="text-center">
                    <a [routerLink]="['/accounting/process-deferred-accounting', item.id]" class="btn btn-outline-primary btn-sm p-1">
                      <i class="fa fa-eye"></i>
                    </a>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="8" class="text-center py-4 text-muted">No Process Deferred Accounting entries found.</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class ProcessDeferredAccountingListComponent implements OnInit {
  private service = inject(ProcessDeferredAccountingService);

  DeferredAccountingType = DeferredAccountingType;
  items: ProcessDeferredAccountingDto[] = [];
  filterText = '';
  selectedType: DeferredAccountingType | null = null;
  fromDate: string | null = null;

  ngOnInit() {
    this.loadList();
  }

  loadList() {
    this.service.getList({
      filter: this.filterText,
      type: this.selectedType,
      fromDate: this.fromDate,
      skipCount: 0,
      maxResultCount: 50,
    }).subscribe(res => {
      this.items = res.items || [];
    });
  }

  resetFilters() {
    this.filterText = '';
    this.selectedType = null;
    this.fromDate = null;
    this.loadList();
  }
}
