import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ApprovalRequestService, ApprovalRequestDto } from '../../proxy/workflow/approval-request.service';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-approval-inbox',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent, StatusBadgeComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">
          <i class="bi bi-inbox me-2"></i>{{ 'MyERP::ApprovalInbox' | abpLocalization }}
          @if (pendingCount() > 0) {
            <span class="badge bg-danger ms-2">{{ pendingCount() }}</span>
          }
        </h5>
        <select class="form-select form-select-sm" style="width: 180px;" [(ngModel)]="filterDocType" (change)="loadData()">
          <option value="">All Document Types</option>
          <option value="SalesInvoice">Sales Invoice</option>
          <option value="PurchaseInvoice">Purchase Invoice</option>
          <option value="PaymentEntry">Payment Entry</option>
          <option value="JournalEntry">Journal Entry</option>
          <option value="StockEntry">Stock Entry</option>
          <option value="ExpenseClaim">Expense Claim</option>
        </select>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'MyERP::DocumentType' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Level' | abpLocalization }}</th>
                  <th>{{ 'MyERP::RequestedOn' | abpLocalization }}</th>
                  <th class="text-center">{{ 'MyERP::Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (request of requests(); track request.id) {
                  <tr>
                    <td>
                      <a [routerLink]="getDocumentLink(request)" class="text-decoration-none">
                        <span class="badge bg-light text-dark me-1">{{ request.documentType }}</span>
                        <span class="small text-muted">{{ request.documentId | slice:0:8 }}...</span>
                      </a>
                    </td>
                    <td><span class="badge bg-info">Level {{ request.level }}</span></td>
                    <td class="small">{{ request.creationTime | date:'medium' }}</td>
                    <td class="text-center">
                      <app-status-badge
                        [status]="getStatusLabel(request.status)"
                        [variant]="getStatusVariant(request.status)" />
                    </td>
                    <td class="text-end">
                      @if (request.status === 0) {
                        <div class="btn-group btn-group-sm">
                          <button class="btn btn-success" (click)="approve(request)" title="Approve">
                            <i class="bi bi-check-lg"></i> Approve
                          </button>
                          <button class="btn btn-outline-danger" (click)="reject(request)" title="Reject">
                            <i class="bi bi-x-lg"></i>
                          </button>
                        </div>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="text-center text-muted py-5">
                      <i class="bi bi-check-circle d-block mb-2" style="font-size: 2rem;"></i>
                      <p>No pending approvals</p>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize" [currentPage]="currentPage"
            (pageChange)="onPageChange($event)" />
        }
      </div>
    </div>
  `,
})
export class ApprovalInboxComponent implements OnInit {
  private service = inject(ApprovalRequestService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  requests = signal<ApprovalRequestDto[]>([]);
  totalCount = signal(0);
  pendingCount = signal(0);
  loading = signal(false);
  filterDocType = '';
  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.loadData();
    this.service.getPendingCount().subscribe(r => this.pendingCount.set(r.totalCount ?? 0));
  }

  loadData() {
    this.loading.set(true);
    this.service.getMyPending({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
    }).subscribe({
      next: (res) => {
        let items = res.items ?? [];
        if (this.filterDocType) {
          items = items.filter(r => r.documentType === this.filterDocType);
        }
        this.requests.set(items);
        this.totalCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  approve(request: ApprovalRequestDto) {
    this.service.approve(request.id).subscribe({
      next: () => {
        this.toaster.success('Approved successfully');
        this.loadData();
        this.pendingCount.update(c => Math.max(0, c - 1));
      },
    });
  }

  reject(request: ApprovalRequestDto) {
    this.confirmation.warn('Reject this approval request?', 'MyERP::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.reject(request.id, 'Rejected by approver').subscribe({
          next: () => {
            this.toaster.success('Rejected');
            this.loadData();
            this.pendingCount.update(c => Math.max(0, c - 1));
          },
        });
      }
    });
  }

  getDocumentLink(request: ApprovalRequestDto): string[] {
    const typeRouteMap: Record<string, string> = {
      SalesInvoice: '/sales/invoices',
      PurchaseInvoice: '/purchasing/invoices',
      PaymentEntry: '/accounting/payments',
      JournalEntry: '/accounting/journal-entries',
      StockEntry: '/inventory/stock-entries',
      ExpenseClaim: '/hr/expense-claims',
    };
    const base = typeRouteMap[request.documentType] ?? '/';
    return [base, request.documentId];
  }

  getStatusLabel(status: number): string {
    return ['Pending', 'Approved', 'Rejected', 'Cancelled'][status] ?? 'Unknown';
  }

  getStatusVariant(status: number): string {
    return ['warning', 'success', 'danger', 'secondary'][status] ?? 'secondary';
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }
}
