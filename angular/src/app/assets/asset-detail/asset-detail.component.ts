import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { FormsModule } from '@angular/forms';
import { AssetService } from '../../proxy/assets/asset.service';
import type { AssetDto } from '../../proxy/assets/models';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto } from '../../proxy/accounting/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-asset-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageModule, LocalizationPipe, DocumentWorkflowComponent, BreadcrumbComponent, ActivityLogComponent, StatusBadgeComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="asset()?.assetName ?? ('::Asset' | abpLocalization)">
      @if (!asset()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="row mb-3">
          <div class="col-md-8">
            <div class="card mb-3">
              <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">{{ asset()!.assetName }}</h5>
                <app-status-badge [status]="asset()!.status + ''" />
              </div>
              <div class="card-body">
                <div class="row g-3">
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::AssetCode' | abpLocalization }}</small><strong>{{ asset()!.assetNumber ?? '—' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::AssetName' | abpLocalization }}</small><strong>{{ asset()!.assetName }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::AssetCategory' | abpLocalization }}</small><strong>{{ asset()!.assetCategoryName ?? '—' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::Location' | abpLocalization }}</small><strong>{{ asset()!.location ?? '—' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::PurchaseDate' | abpLocalization }}</small><strong>{{ asset()!.purchaseDate | date:'dd/MM/yyyy' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::AvailableForUseDate' | abpLocalization }}</small><strong>{{ asset()!.availableForUseDate ? (asset()!.availableForUseDate | date:'dd/MM/yyyy') : '—' }}</strong></div>
                  <div class="col-md-4"><small class="text-muted d-block">{{ '::PurchaseAmount' | abpLocalization }}</small><strong>{{ asset()!.purchaseAmount | number:'1.2-2' }}</strong></div>
                  <div class="col-md-4"><small class="text-muted d-block">{{ '::AdditionalCost' | abpLocalization }}</small><strong>{{ asset()!.additionalCost | number:'1.2-2' }}</strong></div>
                  <div class="col-md-4"><small class="text-muted d-block">{{ '::TotalAssetCost' | abpLocalization }}</small><strong>{{ (asset()!.totalAssetCost || ((asset()!.purchaseAmount || 0) + (asset()!.additionalCost || 0))) | number:'1.2-2' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::BookValue' | abpLocalization }}</small>
                    <strong class="fs-5 text-primary" [class.text-danger]="(asset()!.valueAfterDepreciation ?? 0) <= 0">{{ asset()!.valueAfterDepreciation | number:'1.2-2' }}</strong>
                  </div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::CalculateDepreciation' | abpLocalization }}</small>
                    <span class="badge" [class.bg-success]="asset()!.calculateDepreciation" [class.bg-secondary]="!asset()!.calculateDepreciation">
                      {{ asset()!.calculateDepreciation ? ('::Yes' | abpLocalization) : ('::No' | abpLocalization) }}
                    </span>
                  </div>
                  @if (asset()!.calculateDepreciation) {
                    <div class="col-md-4"><small class="text-muted d-block">{{ '::UsefulLife' | abpLocalization }}</small><strong>{{ asset()!.usefulLifeMonths }} months</strong></div>
                    <div class="col-md-4"><small class="text-muted d-block">{{ '::DepreciationRate' | abpLocalization }}</small><strong>{{ asset()!.depreciationRate }}%</strong></div>
                    <div class="col-md-4"><small class="text-muted d-block">{{ '::Frequency' | abpLocalization }}</small><strong>Every {{ asset()!.frequencyMonths }} months</strong></div>
                  }
                  @if (asset()!.notes) {
                    <div class="col-12"><small class="text-muted d-block">{{ '::Notes' | abpLocalization }}</small><div>{{ asset()!.notes }}</div></div>
                  }
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <app-document-workflow [actions]="getActions()" (actionClicked)="onAction($event)" />
            @if (asset()!.status === 0) {
              <div class="mt-2">
                <a class="btn btn-outline-secondary btn-sm w-100" [routerLink]="['/assets', asset()!.id, 'edit']">
                  <i class="fa fa-edit me-1"></i>{{ '::Edit' | abpLocalization }}
                </a>
              </div>
            }

            @if (showSellForm) {
              <div class="card mt-2">
                <div class="card-body">
                  <h6 class="card-title">{{ '::Sell' | abpLocalization }}</h6>
                  <div class="mb-2">
                    <label class="form-label">{{ '::DisposalDate' | abpLocalization }}</label>
                    <input type="date" class="form-control form-control-sm" [(ngModel)]="sellForm.disposalDate" />
                  </div>
                  <div class="mb-2">
                    <label class="form-label">{{ '::DisposalAmount' | abpLocalization }}</label>
                    <input type="number" class="form-control form-control-sm" [(ngModel)]="sellForm.amount" min="0" />
                  </div>
                  <div class="mb-2">
                    <label class="form-label">{{ '::SettlementAccount' | abpLocalization }}</label>
                    <select class="form-select form-select-sm" [(ngModel)]="sellForm.settlementAccountId">
                      <option value="">-</option>
                      @for (a of accounts; track a.id) { <option [value]="a.id">{{ a.accountName }}</option> }
                    </select>
                  </div>
                  <div class="d-flex gap-2">
                    <button class="btn btn-sm btn-success" [disabled]="sellForm.amount > 0 && !sellForm.settlementAccountId" (click)="confirmSell()">{{ '::Confirm' | abpLocalization }}</button>
                    <button class="btn btn-sm btn-secondary" (click)="showSellForm = false">{{ '::Cancel' | abpLocalization }}</button>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>

        @if ((asset()!.schedule ?? []).length > 0) {
          <div class="card mb-3">
            <div class="card-header"><h6 class="mb-0">{{ '::DepreciationSchedule' | abpLocalization }}</h6></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>#</th>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::AccumulatedDepreciation' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                </tr></thead>
                <tbody>
                  @for (entry of asset()!.schedule ?? []; track entry.id; let i = $index) {
                    <tr [class.table-success]="entry.isBooked">
                      <td>{{ i + 1 }}</td>
                      <td>{{ entry.scheduleDate | date:'dd/MM/yyyy' }}</td>
                      <td class="text-end">{{ entry.depreciationAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ entry.accumulatedDepreciation | number:'1.2-2' }}</td>
                      <td>
                        @if (entry.isBooked) {
                          <span class="badge bg-success"><i class="fa fa-check me-1"></i>{{ '::Booked' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-secondary">{{ '::Pending' | abpLocalization }}</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <app-activity-log documentType="Asset" [documentId]="asset()!.id!" />
      }
    </abp-page>
  `,
})
export class AssetDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(AssetService);
  private accountService = inject(AccountService);
  private confirmation = inject(ConfirmationService);

  asset = signal<AssetDto | null>(null);
  accounts: AccountDto[] = [];
  showSellForm = false;
  sellForm = { disposalDate: new Date().toISOString().substring(0, 10), amount: 0, settlementAccountId: '' };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.service.get(id).subscribe(a => this.asset.set(a));
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' } as any).subscribe({
      next: res => { this.accounts = res.items ?? []; },
      error: () => {},
    });
  }

  getActions(): WorkflowAction[] {
    const s = this.asset()?.status;
    const actions: WorkflowAction[] = [];
    if (s === 0) actions.push({ name: 'submit', label: 'Submit', icon: 'fa-paper-plane', color: 'btn-outline-primary' });
    if (s === 1 || s === 2 || s === 3) actions.push({ name: 'sell', label: 'Sell', icon: 'fa-hand-holding-dollar', color: 'btn-outline-success' });
    if (s === 1 || s === 2 || s === 3) actions.push({ name: 'scrap', label: 'Scrap', icon: 'fa-trash-can', color: 'btn-outline-warning' });
    if (s === 1 || s === 2 || s === 3) actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa-ban', color: 'btn-outline-danger' });
    if (s === 5) actions.push({ name: 'restore', label: 'Restore', icon: 'fa-rotate-left', color: 'btn-outline-primary' });
    return actions;
  }

  onAction(name: string): void {
    const id = this.asset()!.id!;
    const reload = () => this.service.get(id).subscribe(a => this.asset.set(a));
    const today = new Date().toISOString().substring(0, 10);
    switch (name) {
      case 'submit':
        this.service.submit(id).subscribe(reload);
        break;
      case 'sell':
        this.sellForm = { disposalDate: today, amount: 0, settlementAccountId: '' };
        this.showSellForm = true;
        break;
      case 'scrap':
        this.service.scrap(id, today).subscribe(reload);
        break;
      case 'restore':
        this.service.restore(id).subscribe(reload);
        break;
      case 'cancel':
        this.confirmation.warn('::AssetCancelConfirmation', '::AreYouSure').subscribe(status => {
          if (status !== Confirmation.Status.confirm) return;
          this.service.cancel(id).subscribe(reload);
        });
        break;
    }
  }

  confirmSell(): void {
    const id = this.asset()!.id!;
    const reload = () => this.service.get(id).subscribe(a => this.asset.set(a));
    this.service.sell(id, this.sellForm.disposalDate, this.sellForm.amount, this.sellForm.settlementAccountId || null).subscribe(() => {
      this.showSellForm = false;
      reload();
    });
  }
}
