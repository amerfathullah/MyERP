import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import type { BankAccountDto } from '../../proxy/accounting/models';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-bank-account-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    @if (account()) {
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">
            <i class="bi bi-bank me-2"></i>{{ account()!.accountName }}
            @if (account()!.isDefault) {
              <span class="badge bg-warning ms-2">Default</span>
            }
            @if (account()!.isDisabled) {
              <span class="badge bg-secondary ms-2">Disabled</span>
            }
          </h5>
          <div class="btn-group btn-group-sm">
            <a [routerLink]="['edit']" class="btn btn-outline-primary">
              <i class="bi bi-pencil me-1"></i>{{ 'MyERP::Edit' | abpLocalization }}
            </a>
            @if (!account()!.isDefault && !account()!.isDisabled) {
              <button class="btn btn-outline-warning" (click)="setDefault()">
                <i class="bi bi-star me-1"></i>{{ 'MyERP::SetAsDefault' | abpLocalization }}
              </button>
            }
            @if (!account()!.isDisabled) {
              <button class="btn btn-outline-danger" (click)="disable()">
                <i class="bi bi-x-circle me-1"></i>{{ 'MyERP::Disable' | abpLocalization }}
              </button>
            }
            <button class="btn btn-outline-danger" (click)="deleteAccount()">
              <i class="bi bi-trash me-1"></i>{{ 'MyERP::Delete' | abpLocalization }}
            </button>
          </div>
        </div>
        <div class="card-body">
          <div class="row mb-4">
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::BankName' | abpLocalization }}</label>
              <div class="fw-medium">{{ account()!.bankName }}</div>
            </div>
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::BankAccountNo' | abpLocalization }}</label>
              <div class="font-monospace">{{ account()!.bankAccountNo || '—' }}</div>
            </div>
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::Currency' | abpLocalization }}</label>
              <div>{{ account()!.currencyCode }}</div>
            </div>
          </div>
          <div class="row mb-4">
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::Iban' | abpLocalization }}</label>
              <div class="font-monospace">{{ account()!.iban || '—' }}</div>
            </div>
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::SwiftCode' | abpLocalization }}</label>
              <div>{{ account()!.swiftCode || '—' }}</div>
            </div>
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::BranchCode' | abpLocalization }}</label>
              <div>{{ account()!.branchCode || '—' }}</div>
            </div>
          </div>
          <div class="row">
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::IsCompanyAccount' | abpLocalization }}</label>
              <div>{{ account()!.isCompanyAccount ? 'Yes' : 'No' }}</div>
            </div>
            @if (account()!.isCreditCard) {
              <div class="col-md-4">
                <label class="form-label text-muted small">Type</label>
                <div><span class="badge bg-info">Credit Card</span></div>
              </div>
            }
            @if (account()!.partyType) {
              <div class="col-md-4">
                <label class="form-label text-muted small">{{ 'MyERP::PartyType' | abpLocalization }}</label>
                <div>{{ account()!.partyType }} — {{ account()!.partyId }}</div>
              </div>
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class BankAccountDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(BankAccountService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  account = signal<BankAccountDto | null>(null);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: (a) => this.account.set(a) });
  }

  setDefault() {
    this.service.setAsDefault(this.account()!.id).subscribe({
      next: (a) => {
        this.account.set(a);
        this.toaster.success('MyERP::SuccessfullyUpdated');
      },
    });
  }

  disable() {
    this.confirmation.warn('MyERP::AreYouSure', 'MyERP::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.disable(this.account()!.id).subscribe({
          next: (a) => {
            this.account.set(a);
            this.toaster.success('MyERP::SuccessfullyUpdated');
          },
        });
      }
    });
  }

  deleteAccount() {
    this.confirmation.warn('MyERP::AreYouSure', 'MyERP::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.delete(this.account()!.id).subscribe({
          next: () => {
            this.toaster.success('MyERP::SuccessfullyDeleted');
            this.router.navigate(['..'], { relativeTo: this.route });
          },
        });
      }
    });
  }
}
