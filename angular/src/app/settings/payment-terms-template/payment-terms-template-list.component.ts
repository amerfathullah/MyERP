import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators, FormArray } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { Confirmation, ToasterService , ConfirmationService } from '@abp/ng.theme.shared';

@Component({
  standalone: true,
  selector: 'app-payment-terms-template-list',
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-file-contract me-2"></i>{{ '::PaymentTermsTemplates' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showForm = !showForm">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>
        <div class="card-body">
          @if (showForm) {
            <div class="border rounded p-3 mb-3 bg-light">
              <form [formGroup]="form" (ngSubmit)="save()">
                <div class="row g-2 mb-2">
                  <div class="col-md-6">
                    <label class="form-label">{{ '::Name' | abpLocalization }}</label>
                    <input class="form-control form-control-sm" formControlName="name" />
                  </div>
                </div>
                <h6 class="mt-2">{{ '::Terms' | abpLocalization }}</h6>
                <table class="table table-sm">
                  <thead><tr>
                    <th>{{ '::DueDate' | abpLocalization }} (days)</th>
                    <th>{{ '::Portion' | abpLocalization }} (%)</th>
                    <th></th>
                  </tr></thead>
                  <tbody>
                    @for (term of termsArray.controls; track $index) {
                      <tr [formGroup]="$any(term)">
                        <td><input class="form-control form-control-sm" type="number" formControlName="dueDateDays" /></td>
                        <td><input class="form-control form-control-sm" type="number" formControlName="invoicePortion" /></td>
                        <td><button type="button" class="btn btn-outline-danger btn-sm" (click)="removeTerm($index)"><i class="fas fa-times"></i></button></td>
                      </tr>
                    }
                  </tbody>
                </table>
                <button type="button" class="btn btn-outline-secondary btn-sm me-2" (click)="addTerm()"><i class="fas fa-plus me-1"></i>{{ '::AddTerm' | abpLocalization }}</button>
                <button type="submit" class="btn btn-primary btn-sm"><i class="fas fa-save me-1"></i>{{ '::Save' | abpLocalization }}</button>
              </form>
            </div>
          }
          @if (items().length === 0) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-file-contract fa-2x mb-2"></i>
              <p>{{ '::NoPaymentTermsTemplatesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead><tr>
                <th>{{ '::Name' | abpLocalization }}</th>
                <th>{{ '::Terms' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr>
                    <td>{{ item.name }}</td>
                    <td>{{ item.terms?.length || 0 }} terms</td>
                    <td><button class="btn btn-outline-danger btn-sm" (click)="remove(item.id)"><i class="fas fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `
})
export class PaymentTermsTemplateListComponent implements OnInit {
  private http = inject(HttpClient);
  private confirmation = inject(ConfirmationService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);

  items = signal<any[]>([]);
  showForm = false;
  form = this.fb.group({
    name: ['', Validators.required],
  });
  termsArray = new FormArray<any>([]);

  ngOnInit() { this.load(); }

  load() {
    this.http.get<any>('/api/app/payment-terms-template').subscribe({ next: res => this.items.set(res.items ?? []), error: () => {} });
  }

  addTerm() {
    this.termsArray.push(this.fb.group({ dueDateDays: [30], invoicePortion: [100] }));
  }

  removeTerm(i: number) { this.termsArray.removeAt(i); }

  save() {
    if (!this.form.valid) return;
    const dto = { ...this.form.value, terms: this.termsArray.value };
    this.http.post('/api/app/payment-terms-template', dto).subscribe({
      next: () => { this.toaster.success('::SuccessfullySaved'); this.showForm = false; this.termsArray.clear(); this.form.reset(); this.load(); },
      error: () => {}
    });
  }

  remove(id: string) {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.http.delete(`/api/app/payment-terms-template/${id}`).subscribe({ next: () => this.load(), error: () => {} });
    });
  }
}
