import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ShareTransferService } from '../../proxy/accounting/share-transfer.service';
import { ShareholderService } from '../../proxy/accounting/shareholder.service';
import { ShareTypeService } from '../../proxy/accounting/share-type.service';
import { shareTransferTypeOptions, ShareTransferType } from '../../proxy/accounting/share-transfer-type.enum';
import { AccountService } from '../../proxy/accounting/account.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-share-transfer-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'ShareTransfer' : 'NewShareTransfer') | abpLocalization">
      <div class="card"><div class="card-body">
        @if (isEdit() && status !== 0) {
          <div class="alert" [class.alert-success]="status===1" [class.alert-secondary]="status===4">
            {{ 'Status' | abpLocalization }}: {{ statusLabel() }}
          </div>
        }

        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ 'TransferType' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.transferType" [disabled]="readOnly()">
              @for (o of typeOptions; track o.value) { <option [ngValue]="o.value">{{ o.key }}</option> }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Date' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.date" [disabled]="readOnly()" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ShareType' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.shareTypeId" [disabled]="readOnly()">
              <option value="">--</option>
              @for (t of shareTypes(); track t.id) { <option [value]="t.id">{{ t.title }}</option> }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Rate' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.rate" [disabled]="readOnly()" />
          </div>
        </div>

        <div class="row mb-3">
          @if (form.transferType !== issueType) {
            <div class="col-md-6">
              <label class="form-label">{{ 'FromShareholder' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="form.fromShareholderId" [disabled]="readOnly()">
                <option value="">--</option>
                @for (s of shareholders(); track s.id) { <option [value]="s.id">{{ s.title }}</option> }
              </select>
            </div>
          }
          @if (form.transferType !== purchaseType) {
            <div class="col-md-6">
              <label class="form-label">{{ 'ToShareholder' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="form.toShareholderId" [disabled]="readOnly()">
                <option value="">--</option>
                @for (s of shareholders(); track s.id) { <option [value]="s.id">{{ s.title }}</option> }
              </select>
            </div>
          }
        </div>

        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ 'FromNo' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.fromNo" [disabled]="readOnly()" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ToNo' | abpLocalization }}</label>
            <input type="number" class="form-control" [(ngModel)]="form.toNo" [disabled]="readOnly()" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Qty' | abpLocalization }}</label>
            <input type="text" class="form-control" [value]="noOfShares()" disabled />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Amount' | abpLocalization }}</label>
            <input type="text" class="form-control" [value]="amount() | number:'1.2-2'" disabled />
          </div>
        </div>

        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'EquityOrLiabilityAccount' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.equityOrLiabilityAccountId" [disabled]="readOnly()">
              <option value="">--</option>
              @for (a of accounts(); track a.id) { <option [value]="a.id">{{ a.accountCode }} — {{ a.accountName }}</option> }
            </select>
          </div>
          @if (form.transferType === purchaseType) {
            <div class="col-md-6">
              <label class="form-label">{{ 'AssetAccount' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="form.assetAccountId" [disabled]="readOnly()">
                <option value="">--</option>
                @for (a of accounts(); track a.id) { <option [value]="a.id">{{ a.accountCode }} — {{ a.accountName }}</option> }
              </select>
            </div>
          }
        </div>

        <div class="mb-3">
          <label class="form-label">{{ 'Remarks' | abpLocalization }}</label>
          <textarea class="form-control" rows="2" [(ngModel)]="form.remarks" [disabled]="readOnly()"></textarea>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/accounting/share-transfers">{{ 'Cancel' | abpLocalization }}</a>
          @if (!readOnly()) {
            <button class="btn btn-primary" (click)="save()" [disabled]="saving()"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          }
          @if (isEdit() && status === 0) {
            <button class="btn btn-success" (click)="submit()" [disabled]="saving()"><i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}</button>
          }
          @if (isEdit() && status === 1) {
            <button class="btn btn-outline-danger" (click)="cancelTransfer()" [disabled]="saving()"><i class="fa fa-ban me-1"></i>{{ 'CancelTransfer' | abpLocalization }}</button>
          }
        </div>
      </div></div>
    </abp-page>
  `,
})
export class ShareTransferFormComponent implements OnInit {
  private service = inject(ShareTransferService);
  private shareholderService = inject(ShareholderService);
  private shareTypeService = inject(ShareTypeService);
  private accountService = inject(AccountService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  typeOptions = shareTransferTypeOptions;
  issueType = ShareTransferType.Issue;
  purchaseType = ShareTransferType.Purchase;

  saving = signal(false);
  isEdit = signal(false);
  private transferId: string | null = null;
  status = 0;

  shareholders = signal<{ id: string; title: string }[]>([]);
  shareTypes = signal<{ id: string; title: string }[]>([]);
  accounts = signal<{ id: string; accountCode: string; accountName: string }[]>([]);

  form: any = {
    transferType: ShareTransferType.Issue, date: new Date().toISOString().substring(0, 10),
    fromShareholderId: '', toShareholderId: '', shareTypeId: '', fromNo: 1, toNo: 100, rate: 0,
    equityOrLiabilityAccountId: '', assetAccountId: '', remarks: '',
  };

  ngOnInit(): void {
    this.shareholderService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.shareholders.set((r.items ?? []).map(s => ({ id: s.id!, title: s.title! }))));
    this.shareTypeService.getList().subscribe(r =>
      this.shareTypes.set((r.items ?? []).map(t => ({ id: t.id!, title: t.title! }))));
    this.accountService.getList({ maxResultCount: 500, skipCount: 0, sorting: '' } as any).subscribe(r =>
      this.accounts.set((r.items ?? []).map((a: any) => ({ id: a.id, accountCode: a.accountCode, accountName: a.accountName }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.transferId = id;
      this.load(id);
    }
  }

  private load(id: string): void {
    this.service.get(id).subscribe(t => {
      this.status = t.status ?? 0;
      this.form = {
        transferType: t.transferType, date: t.date ? t.date.substring(0, 10) : '',
        fromShareholderId: t.fromShareholderId ?? '', toShareholderId: t.toShareholderId ?? '',
        shareTypeId: t.shareTypeId, fromNo: t.fromNo, toNo: t.toNo, rate: t.rate,
        equityOrLiabilityAccountId: t.equityOrLiabilityAccountId, assetAccountId: t.assetAccountId ?? '',
        remarks: t.remarks ?? '',
      };
    });
  }

  readOnly(): boolean { return this.isEdit() && this.status !== 0; }
  statusLabel(): string { return ['Draft', 'Submitted', '', '', 'Cancelled'][this.status] ?? 'Draft'; }
  noOfShares(): number { return Math.max(0, (this.form.toNo || 0) - (this.form.fromNo || 0) + 1); }
  amount(): number { return this.noOfShares() * (this.form.rate || 0); }

  save(): void {
    this.saving.set(true);
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      transferType: this.form.transferType,
      date: this.form.date,
      fromShareholderId: this.form.fromShareholderId || null,
      toShareholderId: this.form.toShareholderId || null,
      shareTypeId: this.form.shareTypeId,
      fromNo: this.form.fromNo,
      toNo: this.form.toNo,
      rate: this.form.rate,
      equityOrLiabilityAccountId: this.form.equityOrLiabilityAccountId,
      assetAccountId: this.form.assetAccountId || null,
      remarks: this.form.remarks || null,
    };
    const req = this.transferId ? this.service.update(this.transferId, dto) : this.service.create(dto);
    req.subscribe({
      next: (r) => {
        this.saving.set(false);
        this.toaster.success(this.transferId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        if (!this.transferId) this.router.navigate(['/accounting/share-transfers', r.id]);
      },
      error: () => this.saving.set(false),
    });
  }

  submit(): void {
    if (!this.transferId) return;
    this.saving.set(true);
    this.service.submit(this.transferId).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.saving.set(false); this.load(this.transferId!); },
      error: () => this.saving.set(false),
    });
  }

  cancelTransfer(): void {
    if (!this.transferId) return;
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.saving.set(true);
      this.service.cancel(this.transferId!).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.saving.set(false); this.load(this.transferId!); },
        error: () => this.saving.set(false),
      });
    });
  }
}
