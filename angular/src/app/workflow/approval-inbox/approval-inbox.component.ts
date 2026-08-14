import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ApprovalWorkflowService } from '../../proxy/workflow/approval-workflow.service';
import type { ApprovalRequestDto } from '../../proxy/workflow/dtos/models';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-approval-inbox',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent],
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
          <option value="">{{ 'MyERP::AllDocumentTypes' | abpLocalization }}</option>
          <option value="SalesInvoice">{{ 'MyERP::SalesInvoice' | abpLocalization }}</option>
          <option value="PurchaseInvoice">{{ 'MyERP::PurchaseInvoice' | abpLocalization }}</option>
          <option value="PaymentEntry">{{ 'MyERP::PaymentEntry' | abpLocalization }}</option>
          <option value="JournalEntry">{{ 'MyERP::JournalEntry' | abpLocalization }}</option>
          <option value="StockEntry">{{ 'MyERP::StockEntry' | abpLocalization }}</option>
          <option value="ExpenseClaim">{{ 'MyERP::ExpenseClaim' | abpLocalization }}</option>
        </select>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-4">
            <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
          </div>
        } @else if (requests().length === 0) {
          <div class="text-center py-4 text-muted">
            <i class="bi bi-check2-circle fs-1 text-success d-block mb-2"></i>
            {{ 'MyERP::NoPendingApprovals' | abpLocalization }}
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead>
                <tr>
                  <th>{{ 'MyERP::DocumentType' | abpLocalization }}</th>
                  <th>{{ 'MyERP::DocumentNumber' | abpLocalization }}</th>
                  <th>{{ 'MyERP::RequestedBy' | abpLocalization }}</th>
                  <th>{{ 'MyERP::RequestDate' | abpLocalization }}</th>
                  <th>{{ 'MyERP::Notes' | abpLocalization }}</th>
                  <th class="text-end">{{ 'MyERP::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (req of requests(); track req.id) {
                  <tr>
                    <td>
                      <span class="badge bg-secondary">{{ req.documentType }}</span>
                    </td>
                    <td>
                      <a [routerLink]="getDocumentLink(req)" class="fw-bold text-decoration-none">
                        {{ req.documentId }}
                      </a>
                    </td>
                    <td>{{ req.requestedByUserId || '—' }}</td>
                    <td>{{ req.creationTime | date:'medium' }}</td>
                    <td>
                      <small class="text-muted">{{ req.remarks || req.ruleName || '—' }}</small>
                    </td>
                    <td class="text-end">
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-success" (click)="approve(req)" title="Approve">
                          <i class="bi bi-check-lg me-1"></i>{{ 'MyERP::Approve' | abpLocalization }}
                        </button>
                        <button class="btn btn-outline-danger" (click)="reject(req)" title="Reject">
                          <i class="bi bi-x-lg me-1"></i>{{ 'MyERP::Reject' | abpLocalization }}
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <app-pagination
            [totalCount]="totalCount()"
            [currentPage]="currentPage"
            [pageSize]="pageSize"
            (pageChange)="onPageChange($event)">
          </app-pagination>
        }
      </div>
    </div>
  `,
})
export class ApprovalInboxComponent implements OnInit {
  private service = inject(ApprovalWorkflowService);
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
  }

  loadData() {
    this.loading.set(true);
    this.service.getPendingApprovals({
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
        this.pendingCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  approve(request: ApprovalRequestDto) {
    if (!request.id) return;
    this.service.approve({ requestId: request.id }).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyApproved');
        this.loadData();
      },
    });
  }

  reject(request: ApprovalRequestDto) {
    if (!request.id) return;
    this.confirmation.warn('::RejectConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.reject({ requestId: request.id, remarks: 'Rejected by approver' }).subscribe({
          next: () => {
            this.toaster.success('::SuccessfullyRejected');
            this.loadData();
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
    const base = (request.documentType && typeRouteMap[request.documentType]) ? typeRouteMap[request.documentType] : '/';
    return [base, request.documentId || ''];
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
