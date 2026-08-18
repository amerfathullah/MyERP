import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ErpSettingsService } from '../../proxy/settings/erp-settings.service';
import { CompanyService } from '../../proxy/core/company.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-global-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-globe me-2"></i>{{ 'MyERP::GlobalSettings' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
        } @else {
          <form (ngSubmit)="save()">
            <div class="row mb-3">
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::DefaultCompany' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="settings['MyERP.Global.DefaultCompany']" name="defaultCompany">
                  <option value="">—</option>
                  @for (c of companies(); track c.id) {
                    <option [value]="c.id">{{ c.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::DefaultCurrency' | abpLocalization }}</label>
                <input type="text" class="form-control" maxlength="10"
                  [(ngModel)]="settings['MyERP.Global.DefaultCurrency']" name="defaultCurrency" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::Country' | abpLocalization }}</label>
                <input type="text" class="form-control"
                  [(ngModel)]="settings['MyERP.Global.Country']" name="country" />
              </div>
            </div>

            <h6 class="text-muted mb-3 mt-4">{{ 'MyERP::DisplayOptions' | abpLocalization }}</h6>
            <div class="row mb-3">
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="disableRounded"
                    [ngModel]="settings['MyERP.Global.DisableRoundedTotal'] === 'true'"
                    (ngModelChange)="settings['MyERP.Global.DisableRoundedTotal'] = $event ? 'true' : 'false'" name="disableRounded" />
                  <label class="form-check-label" for="disableRounded">{{ 'MyERP::DisableRoundedTotal' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="disableInWords"
                    [ngModel]="settings['MyERP.Global.DisableInWords'] === 'true'"
                    (ngModelChange)="settings['MyERP.Global.DisableInWords'] = $event ? 'true' : 'false'" name="disableInWords" />
                  <label class="form-check-label" for="disableInWords">{{ 'MyERP::DisableInWords' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <div class="form-check form-switch">
                  <input type="checkbox" class="form-check-input" id="hideCurrencySymbol"
                    [ngModel]="settings['MyERP.Global.HideCurrencySymbol'] === 'true'"
                    (ngModelChange)="settings['MyERP.Global.HideCurrencySymbol'] = $event ? 'true' : 'false'" name="hideCurrencySymbol" />
                  <label class="form-check-label" for="hideCurrencySymbol">{{ 'MyERP::HideCurrencySymbol' | abpLocalization }}</label>
                </div>
              </div>
            </div>

            <div class="d-flex justify-content-end mt-4">
              <button type="submit" class="btn btn-primary" [disabled]="saving()">
                <i class="bi bi-check-lg me-1"></i>{{ 'MyERP::Save' | abpLocalization }}
              </button>
            </div>
          </form>
        }
      </div>
    </div>
  `,
})
export class GlobalSettingsComponent implements OnInit {
  private service = inject(ErpSettingsService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  settings: Record<string, string> = {};
  companies = signal<{ id: string; name: string }[]>([]);
  loading = signal(true);
  saving = signal(false);

  ngOnInit() {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' } as any)
      .subscribe(r => this.companies.set((r.items ?? []).map((c: any) => ({ id: c.id!, name: c.name ?? '' }))));

    this.service.getGroup('Global').subscribe({
      next: (data) => { this.settings = data; this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  save() {
    this.saving.set(true);
    this.service.update(this.settings).subscribe({
      next: () => { this.saving.set(false); this.toaster.success('MyERP::SuccessfullySaved'); },
      error: () => this.saving.set(false),
    });
  }
}
