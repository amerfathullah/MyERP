import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

interface PosOpeningDto {
  id: string;
  companyId: string;
  posProfileId: string;
  userId: string;
  openingDate: string;
  status: string;
  totalOpeningAmount: number;
  posClosingEntryId?: string;
  payments: { modeName: string; openingAmount: number }[];
}

@Component({
  selector: 'app-pos-opening-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="container-fluid py-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ '::PosOpeningEntries' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showForm = !showForm">
            <i class="fa fa-plus me-1"></i>{{ '::OpenNewShift' | abpLocalization }}
          </button>
        </div>

        @if (showForm) {
          <div class="card-body border-bottom bg-light">
            <div class="row g-2 mb-2">
              <div class="col-md-4">
                <label class="form-label small">{{ '::PaymentMode' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm" [(ngModel)]="newPayment.modeName"
                  placeholder="Cash">
              </div>
              <div class="col-md-3">
                <label class="form-label small">{{ '::OpeningAmount' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" [(ngModel)]="newPayment.openingAmount">
              </div>
              <div class="col-md-2 d-flex align-items-end">
                <button class="btn btn-outline-secondary btn-sm" (click)="addPaymentMode()">
                  <i class="fa fa-plus"></i>
                </button>
              </div>
            </div>

            @if (pendingPayments.length > 0) {
              <div class="mb-2">
                @for (p of pendingPayments; track p.modeName) {
                  <span class="badge bg-info me-1">{{ p.modeName }}: {{ p.openingAmount | number:'1.2-2' }}</span>
                }
              </div>
            }

            <button class="btn btn-success btn-sm" (click)="openShift()" [disabled]="pendingPayments.length === 0">
              <i class="fa fa-play me-1"></i>{{ '::OpenShift' | abpLocalization }}
            </button>
          </div>
        }

        <div class="card-body p-0">
          @if (entries().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-cash-register fa-2x mb-2"></i>
              <p>{{ '::NoPosOpeningEntriesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th class="text-end">{{ '::OpeningAmount' | abpLocalization }}</th>
                  <th>{{ '::PaymentModes' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (entry of entries(); track entry.id) {
                  <tr>
                    <td>{{ entry.openingDate | date:'dd/MM/yyyy' }}</td>
                    <td>
                      <span class="badge"
                        [class.bg-success]="entry.status === 'Open'"
                        [class.bg-secondary]="entry.status === 'Closed'"
                        [class.bg-danger]="entry.status === 'Cancelled'">
                        {{ entry.status }}
                      </span>
                    </td>
                    <td class="text-end">{{ entry.totalOpeningAmount | number:'1.2-2' }}</td>
                    <td>
                      @for (p of entry.payments; track p.modeName) {
                        <span class="badge bg-light text-dark me-1">{{ p.modeName }}</span>
                      }
                    </td>
                    <td class="text-end">
                      @if (entry.status === 'Closed') {
                        <button class="btn btn-outline-danger btn-sm" (click)="cancel(entry.id)">
                          <i class="fa fa-ban"></i>
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>

        @if (totalCount() > 20) {
          <div class="card-footer">
            <app-pagination [totalCount]="totalCount()" [pageSize]="20" [currentPage]="currentPage"
              (pageChange)="onPageChange($event)"></app-pagination>
          </div>
        }
      </div>
    </div>
  `
})
export class PosOpeningListComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  entries = signal<PosOpeningDto[]>([]);
  totalCount = signal(0);
  currentPage = 0;
  showForm = false;
  newPayment = { modeName: 'Cash', openingAmount: 0 };
  pendingPayments: { modeName: string; openingAmount: number; modeOfPaymentId: string }[] = [];

  ngOnInit() {
    this.load();
  }

  load() {
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { maxResultCount: 20, skipCount: this.currentPage * 20 };
    if (companyId) params.companyId = companyId;

    this.http.get<any>('/api/app/pos-opening', { params }).subscribe(res => {
      this.entries.set(res.items ?? []);
      this.totalCount.set(res.totalCount ?? 0);
    });
  }

  addPaymentMode() {
    if (!this.newPayment.modeName) return;
    this.pendingPayments.push({
      modeName: this.newPayment.modeName,
      openingAmount: this.newPayment.openingAmount || 0,
      modeOfPaymentId: '00000000-0000-0000-0000-000000000000' // placeholder
    });
    this.newPayment = { modeName: '', openingAmount: 0 };
  }

  openShift() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.http.post('/api/app/pos-opening', {
      companyId,
      posProfileId: companyId, // simplified: use company as profile
      userId: '00000000-0000-0000-0000-000000000000', // will be resolved from current user
      payments: this.pendingPayments
    }).subscribe({
      next: () => {
        this.toaster.success('::PosShiftOpened');
        this.showForm = false;
        this.pendingPayments = [];
        this.load();
      }
    });
  }

  cancel(id: string) {
    if (!confirm('Cancel this POS session?')) return;
    this.http.post(`/api/app/pos-opening/${id}/cancel`, {}).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCancelled');
        this.load();
      }
    });
  }

  onPageChange(event: any) {
    this.currentPage = event.pageIndex;
    this.load();
  }
}
