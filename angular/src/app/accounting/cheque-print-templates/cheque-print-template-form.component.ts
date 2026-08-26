import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { ChequePrintTemplateService } from '../../proxy/accounting/cheque-print-template.service';
import { ChequeSize } from '../../proxy/accounting/cheque-size.enum';

@Component({
  selector: 'app-cheque-print-template-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Cheque Print Template</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <!-- Left Column: Form Settings -->
            <div class="col-lg-6">
              <h6 class="text-primary border-bottom pb-2 mb-3">Primary Settings (Dimensions in cm)</h6>

              <div class="mb-3">
                <label class="form-label">Bank Name *</label>
                <input type="text" class="form-control" formControlName="bankName" placeholder="e.g. Maybank, CIMB, Public Bank">
              </div>

              <div class="row">
                <div class="col-md-6 mb-3">
                  <label class="form-label">Cheque Size</label>
                  <select class="form-select" formControlName="chequeSize">
                    <option [ngValue]="0">Regular</option>
                    <option [ngValue]="1">A4</option>
                  </select>
                </div>
                @if (form.get('chequeSize')?.value === 1) {
                  <div class="col-md-6 mb-3">
                    <label class="form-label">Top Edge Offset (cm)</label>
                    <input type="number" step="0.1" class="form-control" formControlName="startingPositionFromTopEdge">
                  </div>
                }
              </div>

              <div class="row">
                <div class="col-md-6 mb-3">
                  <label class="form-label">Cheque Width (cm)</label>
                  <input type="number" step="0.1" class="form-control" formControlName="chequeWidth">
                </div>
                <div class="col-md-6 mb-3">
                  <label class="form-label">Cheque Height (cm)</label>
                  <input type="number" step="0.1" class="form-control" formControlName="chequeHeight">
                </div>
              </div>

              <h6 class="text-primary border-bottom pb-2 my-3">Account Payee Badge</h6>
              <div class="form-check form-switch mb-2">
                <input class="form-check-input" type="checkbox" id="isAccountPayable" formControlName="isAccountPayable">
                <label class="form-check-label" for="isAccountPayable">Is Account Payable Only</label>
              </div>
              @if (form.get('isAccountPayable')?.value) {
                <div class="row">
                  <div class="col-md-4 mb-2">
                    <label class="form-label small">Top (cm)</label>
                    <input type="number" step="0.1" class="form-control form-control-sm" formControlName="accPayDistFromTopEdge">
                  </div>
                  <div class="col-md-4 mb-2">
                    <label class="form-label small">Left (cm)</label>
                    <input type="number" step="0.1" class="form-control form-control-sm" formControlName="accPayDistFromLeftEdge">
                  </div>
                  <div class="col-md-4 mb-2">
                    <label class="form-label small">Message</label>
                    <input type="text" class="form-control form-control-sm" formControlName="messageToShow">
                  </div>
                </div>
              }

              <h6 class="text-primary border-bottom pb-2 my-3">Date & Payee Positioning</h6>
              <div class="row">
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Date Top (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="dateDistFromTopEdge">
                </div>
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Date Left (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="dateDistFromLeftEdge">
                </div>
              </div>
              <div class="row">
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Payee Name Top (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="payerNameFromTopEdge">
                </div>
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Payee Name Left (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="payerNameFromLeftEdge">
                </div>
              </div>

              <h6 class="text-primary border-bottom pb-2 my-3">Amount Positioning</h6>
              <div class="row">
                <div class="col-md-3 mb-2">
                  <label class="form-label small">Words Top</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="amtInWordsFromTopEdge">
                </div>
                <div class="col-md-3 mb-2">
                  <label class="form-label small">Words Left</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="amtInWordsFromLeftEdge">
                </div>
                <div class="col-md-3 mb-2">
                  <label class="form-label small">Words Width</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="amtInWordWidth">
                </div>
                <div class="col-md-3 mb-2">
                  <label class="form-label small">Line Spacing</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="amtInWordsLineSpacing">
                </div>
              </div>
              <div class="row">
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Figures Top (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="amtInFiguresFromTopEdge">
                </div>
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Figures Left (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="amtInFiguresFromLeftEdge">
                </div>
              </div>

              <h6 class="text-primary border-bottom pb-2 my-3">Account No & Signatory Positioning</h6>
              <div class="row">
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Account No Top (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="accNoDistFromTopEdge">
                </div>
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Account No Left (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="accNoDistFromLeftEdge">
                </div>
              </div>
              <div class="row mb-3">
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Signatory Top (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="signatoryFromTopEdge">
                </div>
                <div class="col-md-6 mb-2">
                  <label class="form-label small">Signatory Left (cm)</label>
                  <input type="number" step="0.1" class="form-control form-control-sm" formControlName="signatoryFromLeftEdge">
                </div>
              </div>

              <div class="d-flex gap-2 mt-4">
                <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
                <a routerLink="/accounting/cheque-print-templates" class="btn btn-secondary">Cancel</a>
              </div>
            </div>

            <!-- Right Column: Interactive Live Preview -->
            <div class="col-lg-6">
              <h6 class="text-secondary border-bottom pb-2 mb-3">Live Cheque Layout Preview</h6>
              <div class="overflow-auto border p-2 bg-light rounded" style="max-height: 600px;">
                <div [style.width.cm]="form.value.chequeWidth || 20"
                     [style.height.cm]="form.value.chequeHeight || 9"
                     class="bg-white border position-relative shadow-sm mx-auto my-2"
                     style="box-sizing: border-box; overflow: hidden; font-family: monospace; font-size: 11px;">

                  <!-- Account Payee -->
                  @if (form.value.isAccountPayable) {
                    <span [style.top.cm]="form.value.accPayDistFromTopEdge || 1"
                          [style.left.cm]="form.value.accPayDistFromLeftEdge || 9"
                          class="position-absolute border-top border-bottom text-center px-1"
                          style="min-width: 2cm; font-size: 10px; font-weight: bold;">
                      {{ form.value.messageToShow || 'Acc. Payee' }}
                    </span>
                  }

                  <!-- Date -->
                  <span [style.top.cm]="form.value.dateDistFromTopEdge || 1"
                        [style.left.cm]="form.value.dateDistFromLeftEdge || 15"
                        class="position-absolute text-muted">
                    [DD-MM-YYYY]
                  </span>

                  <!-- Payee Name -->
                  <span [style.top.cm]="form.value.payerNameFromTopEdge || 2"
                        [style.left.cm]="form.value.payerNameFromLeftEdge || 3"
                        class="position-absolute fw-semibold">
                    [Payee Name Example Sdn Bhd]
                  </span>

                  <!-- Amount in Words -->
                  <span [style.top.cm]="form.value.amtInWordsFromTopEdge || 3"
                        [style.left.cm]="form.value.amtInWordsFromLeftEdge || 4"
                        [style.width.cm]="form.value.amtInWordWidth || 15"
                        [style.line-height.cm]="form.value.amtInWordsLineSpacing || 0.5"
                        class="position-absolute text-dark"
                        style="word-wrap: break-word;">
                    [Ringgit Malaysia: Five Thousand Four Hundred And Thirty Two Only]
                  </span>

                  <!-- Amount in Figures -->
                  <span [style.top.cm]="form.value.amtInFiguresFromTopEdge || 3.5"
                        [style.left.cm]="form.value.amtInFiguresFromLeftEdge || 16"
                        class="position-absolute fw-bold">
                    **5,432.00#
                  </span>

                  <!-- Account No -->
                  <span [style.top.cm]="form.value.accNoDistFromTopEdge || 5"
                        [style.left.cm]="form.value.accNoDistFromLeftEdge || 4"
                        class="position-absolute text-muted small">
                    A/C: 123456789012
                  </span>

                  <!-- Signatory -->
                  <span [style.top.cm]="form.value.signatoryFromTopEdge || 6"
                        [style.left.cm]="form.value.signatoryFromLeftEdge || 15"
                        class="position-absolute text-muted small">
                    For Company Name Sdn Bhd
                  </span>
                </div>
              </div>
              <small class="text-muted d-block mt-2">
                Dimensions and locations scale dynamically in cm based on inputs above.
              </small>
            </div>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ChequePrintTemplateFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ChequePrintTemplateService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      bankName: ['', [Validators.required, Validators.maxLength(100)]],
      chequeSize: [ChequeSize.Regular],
      startingPositionFromTopEdge: [0.0],
      chequeWidth: [20.0, Validators.required],
      chequeHeight: [9.0, Validators.required],
      scannedCheque: [''],
      isAccountPayable: [true],
      accPayDistFromTopEdge: [1.0],
      accPayDistFromLeftEdge: [9.0],
      messageToShow: ['Acc. Payee'],
      dateDistFromTopEdge: [1.0],
      dateDistFromLeftEdge: [15.0],
      payerNameFromTopEdge: [2.0],
      payerNameFromLeftEdge: [3.0],
      amtInWordsFromTopEdge: [3.0],
      amtInWordsFromLeftEdge: [4.0],
      amtInWordWidth: [15.0],
      amtInWordsLineSpacing: [0.5],
      amtInFiguresFromTopEdge: [3.5],
      amtInFiguresFromLeftEdge: [16.0],
      accNoDistFromTopEdge: [5.0],
      accNoDistFromLeftEdge: [4.0],
      signatoryFromTopEdge: [6.0],
      signatoryFromLeftEdge: [15.0],
      hasPrintFormat: [false],
    });
  }

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue(res);
      });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/accounting/cheque-print-templates']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
