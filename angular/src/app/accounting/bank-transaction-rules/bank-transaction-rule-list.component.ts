import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { BankTransactionRuleService } from '../../proxy/accounting/bank-transaction-rule.service';

@Component({
  selector: 'app-bank-transaction-rule-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'BankTransactionRules' | abpLocalization">
      <div class="d-flex justify-content-end mb-3">
        <button class="btn btn-primary btn-sm" (click)="showCreateForm = !showCreateForm">
          <i class="fa fa-plus me-1"></i>{{ 'NewRule' | abpLocalization }}
        </button>
      </div>

      @if (showCreateForm) {
        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">{{ 'NewRule' | abpLocalization }}</h6>
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'Name' | abpLocalization }}</label>
              <input type="text" class="form-control form-control-sm" [(ngModel)]="newRule.name" />
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'TransactionType' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="newRule.transactionType">
                <option [value]="0">Any</option>
                <option [value]="1">Withdrawal</option>
                <option [value]="2">Deposit</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label">Min Amount</label>
              <input type="number" class="form-control form-control-sm" [(ngModel)]="newRule.minAmount" />
            </div>
            <div class="col-md-2">
              <label class="form-label">Max Amount</label>
              <input type="number" class="form-control form-control-sm" [(ngModel)]="newRule.maxAmount" />
            </div>
          </div>
          <div class="row g-3 mt-1">
            <div class="col-md-4">
              <label class="form-label">Description Contains</label>
              <input type="text" class="form-control form-control-sm" [(ngModel)]="newRule.descriptionContains" placeholder="Match text in description" />
            </div>
            <div class="col-md-3">
              <label class="form-label">Classify As</label>
              <select class="form-select form-select-sm" [(ngModel)]="newRule.classifyAs">
                <option [value]="0">Bank Entry</option>
                <option [value]="1">Payment Entry</option>
                <option [value]="2">Transfer</option>
              </select>
            </div>
          </div>
          <div class="mt-3">
            <button class="btn btn-sm btn-primary" (click)="createRule()">{{ 'Save' | abpLocalization }}</button>
            <button class="btn btn-sm btn-outline-secondary ms-2" (click)="showCreateForm = false">{{ 'Cancel' | abpLocalization }}</button>
          </div>
        </div></div>
      }

      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-robot fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoBankTransactionRulesYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th style="width:50px">{{ 'Priority' | abpLocalization }}</th>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'TransactionType' | abpLocalization }}</th>
              <th>Classify As</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (rule of items; track rule.id) {
                <tr>
                  <td class="text-center"><span class="badge bg-secondary">{{ rule.priority }}</span></td>
                  <td>{{ rule.name }}</td>
                  <td>{{ ['Any', 'Withdrawal', 'Deposit'][rule.transactionType ?? 0] }}</td>
                  <td>{{ ['Bank Entry', 'Payment Entry', 'Transfer'][rule.classifyAs ?? 0] }}</td>
                  <td>
                    @if (rule.isEnabled) { <span class="badge bg-success">Active</span> }
                    @else { <span class="badge bg-secondary">Disabled</span> }
                  </td>
                  <td>
                    <button class="btn btn-sm" [class]="rule.isEnabled ? 'btn-outline-warning' : 'btn-outline-success'"
                      (click)="toggleRule(rule)">
                      <i class="fa" [class.fa-pause]="rule.isEnabled" [class.fa-play]="!rule.isEnabled"></i>
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>

        <div class="mt-3">
          <button class="btn btn-sm btn-outline-primary" (click)="evaluateRules()">
            <i class="fa fa-bolt me-1"></i>Evaluate All Unmatched
          </button>
        </div>
      }
    </abp-page>
  `
})
export class BankTransactionRuleListComponent implements OnInit {
  private bankTransactionRuleService = inject(BankTransactionRuleService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  items: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 50;
  currentPage = 0;
  showCreateForm = false;
  newRule: any = { name: '', transactionType: 0, minAmount: null, maxAmount: null, descriptionContains: '', classifyAs: 0 };

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    const cid = this.companyContext.currentCompanyId();
    if (cid) params.companyId = cid;
    this.bankTransactionRuleService.getList(params).subscribe({
      next: res => { this.items = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  createRule() {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) { this.toaster.warn('Select a company first'); return; }
    const dto = { ...this.newRule, companyId: cid, transactionType: Number(this.newRule.transactionType), classifyAs: Number(this.newRule.classifyAs) };
    this.bankTransactionRuleService.create(dto as any).subscribe({
      next: () => { this.toaster.success('Rule created'); this.showCreateForm = false; this.loadData(); },
      error: () => {}
    });
  }

  toggleRule(rule: any) {
    const action$ = rule.isEnabled
      ? this.bankTransactionRuleService.disable(rule.id)
      : this.bankTransactionRuleService.enable(rule.id);
    action$.subscribe({
      next: () => { this.toaster.success(rule.isEnabled ? 'Rule disabled' : 'Rule enabled'); this.loadData(); },
      error: () => {}
    });
  }

  evaluateRules() {
    const cid = this.companyContext.currentCompanyId();
    if (!cid) { this.toaster.warn('Select a company first'); return; }
    this.bankTransactionRuleService.evaluateRules({ companyId: cid } as any).subscribe({
      next: (res: any) => { this.toaster.success(`Matched ${res?.matchedCount ?? 0} transactions`); },
      error: () => {}
    });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.pageSize = e.pageSize; this.loadData(); }
}
