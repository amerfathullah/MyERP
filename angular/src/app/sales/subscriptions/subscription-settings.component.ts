import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { SubscriptionSettingsService } from '../../proxy/accounting/subscription-settings.service';

@Component({
  selector: 'app-subscription-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Subscription Settings</h5>
        <a routerLink="/sales/subscriptions" class="btn btn-secondary btn-sm">Back to Subscriptions</a>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Grace Period (Days)</label>
              <input type="number" class="form-control" formControlName="gracePeriod" placeholder="1">
              <div class="form-text">Number of days after invoice due date before marking as unpaid or canceling.</div>
            </div>

            <div class="col-md-6 mb-3 d-flex flex-column justify-content-center">
              <div class="form-check form-switch mb-3">
                <input class="form-check-input" type="checkbox" id="cancelAfterGrace" formControlName="cancelAfterGrace">
                <label class="form-check-label fw-semibold" for="cancelAfterGrace">Cancel Subscription After Grace Period</label>
              </div>

              <div class="form-check form-switch">
                <input class="form-check-input" type="checkbox" id="prorate" formControlName="prorate">
                <label class="form-check-label fw-semibold" for="prorate">Prorate Charges for Partial Periods</label>
              </div>
            </div>
          </div>

          <button type="submit" class="btn btn-primary mt-3">Save Settings</button>
        </form>
      </div>
    </div>
  `
})
export class SubscriptionSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(SubscriptionSettingsService);
  private toaster = inject(ToasterService);

  form: FormGroup;

  constructor() {
    this.form = this.fb.group({
      gracePeriod: [1],
      cancelAfterGrace: [false],
      prorate: [true],
    });
  }

  ngOnInit() {
    this.service.get().subscribe(res => {
      this.form.patchValue(res);
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
