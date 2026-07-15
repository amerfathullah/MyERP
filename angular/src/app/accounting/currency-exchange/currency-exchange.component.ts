import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

interface CurrencyExchangeDto {
  id: string;
  fromCurrency: string;
  toCurrency: string;
  exchangeRate: number;
  date: string;
}

@Component({
  selector: 'app-currency-exchange',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'CurrencyExchangeRates' | abpLocalization">
      <!-- Add new rate form -->
      <div class="card mb-4">
        <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-plus me-2"></i>{{ 'AddRate' | abpLocalization }}</h6></div>
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="addRate()" class="row g-3 align-items-end">
            <div class="col-md-2">
              <label class="form-label">{{ 'FromCurrency' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="fromCurrency" maxlength="3" placeholder="USD" />
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'ToCurrency' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="toCurrency" maxlength="3" placeholder="MYR" />
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'ExchangeRate' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="exchangeRate" step="0.000001" min="0.000001" />
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'Date' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="date" />
            </div>
            <div class="col-md-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
                <i class="fa fa-plus me-1"></i>{{ 'Add' | abpLocalization }}
              </button>
            </div>
          </form>
        </div>
      </div>

      <!-- Existing rates -->
      @if (isLoading()) {
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      } @else if (rates().length === 0) {
        <div class="text-center py-5 text-muted">
          <i class="fa fa-exchange-alt fa-3x mb-3"></i>
          <p>{{ 'NoRatesYet' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="card">
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th class="ps-3">{{ 'FromCurrency' | abpLocalization }}</th>
                  <th>{{ 'ToCurrency' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ExchangeRate' | abpLocalization }}</th>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th class="pe-3"></th>
                </tr>
              </thead>
              <tbody>
                @for (rate of rates(); track rate.id) {
                  <tr>
                    <td class="ps-3 fw-bold">{{ rate.fromCurrency }}</td>
                    <td>{{ rate.toCurrency }}</td>
                    <td class="text-end font-monospace">{{ rate.exchangeRate | number:'1.6-6' }}</td>
                    <td>{{ rate.date | date:'dd/MM/yyyy' }}</td>
                    <td class="pe-3 text-end">
                      <button class="btn btn-sm btn-outline-danger" (click)="deleteRate(rate.id)"><i class="fa fa-trash"></i></button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class CurrencyExchangeComponent implements OnInit {
  private fb = inject(FormBuilder);
  private restService = inject(RestService);
  private toaster = inject(ToasterService);

  rates = signal<CurrencyExchangeDto[]>([]);
  isLoading = signal(true);

  form = this.fb.group({
    fromCurrency: ['USD', [Validators.required, Validators.maxLength(3)]],
    toCurrency: ['MYR', [Validators.required, Validators.maxLength(3)]],
    exchangeRate: [4.5, [Validators.required, Validators.min(0.000001)]],
    date: [new Date().toISOString().split('T')[0], Validators.required],
  });

  ngOnInit(): void { this.loadRates(); }

  loadRates(): void {
    this.restService.request<any, any>({ method: 'GET', url: '/api/app/currency-exchange', params: { skipCount: '0', maxResultCount: '100' } }, { apiName: 'Default' })
      .subscribe({
        next: res => { this.rates.set(res.items ?? res ?? []); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }

  addRate(): void {
    if (this.form.invalid) return;
    this.restService.request<any, CurrencyExchangeDto>({ method: 'POST', url: '/api/app/currency-exchange', body: this.form.getRawValue() }, { apiName: 'Default' })
      .subscribe({
        next: () => { this.toaster.success('Rate added'); this.loadRates(); },
        error: () => this.toaster.error('Failed to add rate'),
      });
  }

  deleteRate(id: string): void {
    this.restService.request<any, void>({ method: 'DELETE', url: `/api/app/currency-exchange/${id}` }, { apiName: 'Default' }).subscribe({
      next: () => { this.toaster.success('Rate deleted'); this.loadRates(); },
      error: () => this.toaster.error('Failed to delete'),
    });
  }
}
