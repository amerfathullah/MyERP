import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { CurrencyExchangeSettingsService } from '../../proxy/accounting/currency-exchange-settings.service';
import type { TestCurrencyExchangeApiResponseDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-currency-exchange-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Currency Exchange Settings</h5>
        <div class="d-flex gap-2">
          <button type="button" class="btn btn-outline-info btn-sm" (click)="testApi()">
            <i class="fa fa-plug me-1"></i>Test Connection
          </button>
          <button type="button" class="btn btn-primary btn-sm" (click)="save()">Save Settings</button>
        </div>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Service Provider</label>
              <select class="form-select" formControlName="serviceProvider" (change)="onProviderChange()">
                <option value="frankfurter.dev">frankfurter.dev</option>
                <option value="frankfurter.dev - v2">frankfurter.dev - v2</option>
                <option value="exchangerate.host">exchangerate.host</option>
                <option value="Custom">Custom</option>
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Access Key (if required)</label>
              <input type="text" class="form-control" formControlName="accessKey" placeholder="Access key or token">
            </div>

            <div class="col-12 mb-3">
              <label class="form-label">API Endpoint</label>
              <input type="text" class="form-control" formControlName="apiEndpoint"
                placeholder="https://api.frankfurter.dev/v1/{transaction_date}">
              <div class="form-text">Available tokens: <code>&#123;transaction_date&#125;</code>, <code>&#123;from_currency&#125;</code>, <code>&#123;to_currency&#125;</code></div>
            </div>

            <div class="col-md-6 mb-3">
              <div class="form-check form-switch mb-2">
                <input class="form-check-input" type="checkbox" id="useHttp" formControlName="useHttp">
                <label class="form-check-label" for="useHttp">Use HTTP Protocol</label>
              </div>
              <div class="form-check form-switch">
                <input class="form-check-input" type="checkbox" id="disabled" formControlName="disabled">
                <label class="form-check-label" for="disabled">Disabled</label>
              </div>
            </div>
          </div>

          <!-- Request Parameters Table -->
          <div class="mt-4">
            <div class="d-flex justify-content-between align-items-center mb-2">
              <h6 class="fw-bold mb-0">Request Parameters</h6>
              <button type="button" class="btn btn-outline-primary btn-sm" (click)="addParam()">
                <i class="fa fa-plus me-1"></i>Add Parameter
              </button>
            </div>
            <table class="table table-bordered table-sm align-middle">
              <thead>
                <tr>
                  <th style="width:40%">Key</th>
                  <th style="width:50%">Value</th>
                  <th style="width:10%" class="text-center">Action</th>
                </tr>
              </thead>
              <tbody formArrayName="reqParams">
                @for (p of reqParams.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td><input type="text" class="form-control form-control-sm" formControlName="key"></td>
                    <td><input type="text" class="form-control form-control-sm" formControlName="value"></td>
                    <td class="text-center">
                      <button type="button" class="btn btn-danger btn-sm p-1" (click)="removeParam(i)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Result Keys Table -->
          <div class="mt-4">
            <div class="d-flex justify-content-between align-items-center mb-2">
              <h6 class="fw-bold mb-0">Result Keys (Response Navigation Path)</h6>
              <button type="button" class="btn btn-outline-primary btn-sm" (click)="addResultKey()">
                <i class="fa fa-plus me-1"></i>Add Result Key
              </button>
            </div>
            <table class="table table-bordered table-sm align-middle">
              <thead>
                <tr>
                  <th style="width:90%">Key</th>
                  <th style="width:10%" class="text-center">Action</th>
                </tr>
              </thead>
              <tbody formArrayName="resultKeys">
                @for (r of resultKeys.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td><input type="text" class="form-control form-control-sm" formControlName="key"></td>
                    <td class="text-center">
                      <button type="button" class="btn btn-danger btn-sm p-1" (click)="removeResultKey(i)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Test Output Alert -->
          @if (testResult) {
            <div class="mt-4 alert" [ngClass]="testResult.success ? 'alert-success' : 'alert-danger'">
              <h6 class="alert-heading fw-bold">
                {{ testResult.success ? 'Test Successful!' : 'Test Failed' }}
              </h6>
              @if (testResult.success) {
                <p class="mb-1"><strong>Resolved Rate (USD to MYR):</strong> {{ testResult.exchangeRate }}</p>
                <p class="mb-1"><strong>URL:</strong> <code>{{ testResult.resolvedUrl }}</code></p>
              } @else {
                <p class="mb-1"><strong>Error:</strong> {{ testResult.errorMessage }}</p>
                @if (testResult.resolvedUrl) {
                  <p class="mb-0"><strong>URL:</strong> <code>{{ testResult.resolvedUrl }}</code></p>
                }
              }
            </div>
          }

          <div class="mt-4 border-top pt-3">
            <button type="submit" class="btn btn-primary">Save Settings</button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class CurrencyExchangeSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(CurrencyExchangeSettingsService);
  private toaster = inject(ToasterService);

  form: FormGroup;
  testResult: TestCurrencyExchangeApiResponseDto | null = null;

  constructor() {
    this.form = this.fb.group({
      serviceProvider: ['frankfurter.dev', Validators.required],
      apiEndpoint: ['https://api.frankfurter.dev/v1/{transaction_date}', Validators.required],
      accessKey: [''],
      url: [''],
      useHttp: [false],
      disabled: [false],
      reqParams: this.fb.array([]),
      resultKeys: this.fb.array([]),
    });
  }

  get reqParams(): FormArray {
    return this.form.get('reqParams') as FormArray;
  }

  get resultKeys(): FormArray {
    return this.form.get('resultKeys') as FormArray;
  }

  ngOnInit() {
    this.service.get().subscribe(res => {
      this.form.patchValue({
        serviceProvider: res.serviceProvider,
        apiEndpoint: res.apiEndpoint,
        accessKey: res.accessKey,
        url: res.url,
        useHttp: res.useHttp,
        disabled: res.disabled,
      });

      this.reqParams.clear();
      if (res.reqParams) {
        res.reqParams.forEach(p => {
          this.reqParams.push(this.fb.group({ key: [p.key], value: [p.value] }));
        });
      }

      this.resultKeys.clear();
      if (res.resultKeys) {
        res.resultKeys.forEach(r => {
          this.resultKeys.push(this.fb.group({ key: [r.key] }));
        });
      }
    });
  }

  onProviderChange() {
    const p = this.form.get('serviceProvider')?.value;
    const useHttp = this.form.get('useHttp')?.value;
    const proto = useHttp ? 'http://' : 'https://';

    if (p === 'frankfurter.dev') {
      this.form.patchValue({
        apiEndpoint: `${proto}api.frankfurter.dev/v1/{transaction_date}`,
      });
      this.reqParams.clear();
      this.reqParams.push(this.fb.group({ key: ['base'], value: ['{from_currency}'] }));
      this.reqParams.push(this.fb.group({ key: ['symbols'], value: ['{to_currency}'] }));
      this.resultKeys.clear();
      this.resultKeys.push(this.fb.group({ key: ['rates'] }));
      this.resultKeys.push(this.fb.group({ key: ['{to_currency}'] }));
    } else if (p === 'frankfurter.dev - v2') {
      this.form.patchValue({
        apiEndpoint: `${proto}api.frankfurter.dev/v2/rate/{from_currency}/{to_currency}`,
      });
      this.reqParams.clear();
      this.reqParams.push(this.fb.group({ key: ['date'], value: ['{transaction_date}'] }));
      this.resultKeys.clear();
      this.resultKeys.push(this.fb.group({ key: ['rate'] }));
    } else if (p === 'exchangerate.host') {
      this.form.patchValue({
        apiEndpoint: `${proto}api.exchangerate.host/convert`,
      });
      this.reqParams.clear();
      this.reqParams.push(this.fb.group({ key: ['access_key'], value: [this.form.get('accessKey')?.value ?? ''] }));
      this.reqParams.push(this.fb.group({ key: ['amount'], value: ['1'] }));
      this.reqParams.push(this.fb.group({ key: ['date'], value: ['{transaction_date}'] }));
      this.reqParams.push(this.fb.group({ key: ['from'], value: ['{from_currency}'] }));
      this.reqParams.push(this.fb.group({ key: ['to'], value: ['{to_currency}'] }));
      this.resultKeys.clear();
      this.resultKeys.push(this.fb.group({ key: ['result'] }));
    }
  }

  addParam() {
    this.reqParams.push(this.fb.group({ key: [''], value: [''] }));
  }

  removeParam(i: number) {
    this.reqParams.removeAt(i);
  }

  addResultKey() {
    this.resultKeys.push(this.fb.group({ key: [''] }));
  }

  removeResultKey(i: number) {
    this.resultKeys.removeAt(i);
  }

  testApi() {
    this.service.testConnection({
      fromCurrency: 'USD',
      toCurrency: 'MYR',
      transactionDate: new Date().toISOString().split('T')[0],
    }).subscribe({
      next: (res) => {
        this.testResult = res;
      },
      error: (err) => {
        this.testResult = {
          success: false,
          exchangeRate: 0,
          errorMessage: err?.error?.error?.message ?? 'Network error',
        };
      }
    });
  }

  save() {
    this.service.update(this.form.value).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
