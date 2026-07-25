import { Component, Input, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CompanyRestrictionService } from '../../../proxy/core/company-restriction.service';import { CompanyService } from '../../../proxy/core/company.service';import { LocalizationPipe, PermissionService } from '@abp/ng.core';

/**
 * Shared component for managing company restrictions on master data (Item, Customer, Supplier).
 * Shows a toggle + multi-select company list when restriction is enabled.
 * Per ERPNext PR #57383: only visible to users with CompanyRestrictions.Manage permission (manager-level).
 * 
 * Usage: <app-company-restriction parentType="Item" [parentId]="itemId" />
 */
@Component({
  selector: 'app-company-restriction',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    @if (hasManagePermission()) {
    <div class="card mt-3">
      <div class="card-header d-flex align-items-center justify-content-between">
        <h6 class="mb-0"><i class="fa fa-lock me-2"></i>{{ '::CompanyRestriction' | abpLocalization }}</h6>
        <div class="form-check form-switch">
          <input class="form-check-input" type="checkbox" id="restrictToggle"
                 [(ngModel)]="restrictToCompanies"
                 (ngModelChange)="onToggleChanged()">
          <label class="form-check-label" for="restrictToggle">
            {{ '::RestrictToCompanies' | abpLocalization }}
          </label>
        </div>
      </div>
      @if (restrictToCompanies) {
        <div class="card-body">
          <p class="text-muted small mb-2">
            {{ '::CompanyRestrictionHelp' | abpLocalization }}
          </p>
          <div class="row">
            @for (company of availableCompanies(); track company.id) {
              <div class="col-md-6 col-lg-4 mb-2">
                <div class="form-check">
                  <input class="form-check-input" type="checkbox"
                         [id]="'company-' + company.id"
                         [checked]="isSelected(company.id)"
                         (change)="toggleCompany(company.id)">
                  <label class="form-check-label" [for]="'company-' + company.id">
                    {{ company.name }}
                  </label>
                </div>
              </div>
            }
          </div>
          @if (availableCompanies().length === 0) {
            <p class="text-muted">{{ '::NoCompaniesAvailable' | abpLocalization }}</p>
          }
          <div class="mt-3">
            <button class="btn btn-sm btn-primary" (click)="save()" [disabled]="saving()">
              @if (saving()) {
                <span class="spinner-border spinner-border-sm me-1"></span>
              }
              {{ '::Save' | abpLocalization }}
            </button>
          </div>
        </div>
      }
    </div>
    }
  `
})
export class CompanyRestrictionComponent implements OnInit {
  @Input() parentType: string = '';
  @Input() parentId: string = '';

  restrictToCompanies = false;
  selectedCompanyIds = new Set<string>();
  availableCompanies = signal<{ id: string; name: string }[]>([]);
  saving = signal(false);
  hasManagePermission = signal(false);

  private companyService = inject(CompanyService);
  private companyRestrictionService = inject(CompanyRestrictionService);
  private permissionService = inject(PermissionService);

  ngOnInit() {
    // Per ERPNext PR #57383: only master-manager roles can view/edit company restriction fields
    this.hasManagePermission.set(
      this.permissionService.getGrantedPolicy('MyERP.CompanyRestrictions.Manage')
    );

    if (this.hasManagePermission() && this.parentId && this.parentType) {
      this.loadCompanies();
      this.loadRestriction();
    }
  }

  private loadCompanies() {
    this.companyService.getList({ maxResultCount: 200, skipCount: 0, sorting: '' })
      .subscribe(res => {
        this.availableCompanies.set(
          (res.items || []).map((c: any) => ({ id: c.id, name: c.name }))
        );
      });
  }

  private loadRestriction() {
    this.companyRestrictionService.get(this.parentType, this.parentId)
      .subscribe({
        next: (res) => {
          this.restrictToCompanies = res.restrictToCompanies;
          this.selectedCompanyIds = new Set(
            (res.allowedCompanies || []).map((c: any) => c.companyId)
          );
        },
        error: () => {} // Silently handle missing restriction
      });
  }

  isSelected(companyId: string): boolean {
    return this.selectedCompanyIds.has(companyId);
  }

  toggleCompany(companyId: string) {
    if (this.selectedCompanyIds.has(companyId)) {
      this.selectedCompanyIds.delete(companyId);
    } else {
      this.selectedCompanyIds.add(companyId);
    }
  }

  onToggleChanged() {
    if (!this.restrictToCompanies) {
      this.selectedCompanyIds.clear();
      this.save();
    }
  }

  save() {
    this.saving.set(true);
    const dto = {
      parentType: this.parentType,
      parentId: this.parentId,
      restrictToCompanies: this.restrictToCompanies,
      allowedCompanyIds: Array.from(this.selectedCompanyIds)
    };

    this.companyRestrictionService.save(dto as any)
      .subscribe({
        next: () => this.saving.set(false),
        error: () => this.saving.set(false)
      });
  }
}
