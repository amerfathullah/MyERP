import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PromotionalSchemeService } from '../../proxy/sales/promotional-scheme.service';
import { pricingRuleApplyOnOptions, PricingRuleApplyOn } from '../../proxy/sales/pricing-rule-apply-on.enum';
import { pricingRuleTypeOptions, PricingRuleType } from '../../proxy/sales/pricing-rule-type.enum';
import { promotionalSchemeApplicableForOptions, PromotionalSchemeApplicableFor } from '../../proxy/sales/promotional-scheme-applicable-for.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { ItemGroupService } from '../../proxy/inventory/item-group.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

@Component({
  selector: 'app-promotional-scheme-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'Edit' : 'NewPromotionalScheme') | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Title' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.title" />
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'ApplyOn' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.applyOn">
              @for (o of applyOnOptions; track o.value) {
                <option [ngValue]="o.value">{{ o.key }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ApplicableFor' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.applicableFor">
              @for (o of applicableForOptions; track o.value) {
                <option [ngValue]="o.value">{{ o.key }}</option>
              }
            </select>
          </div>
          <div class="col-md-3 d-flex align-items-end gap-3">
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="selling" [(ngModel)]="form.selling" />
              <label class="form-check-label" for="selling">{{ 'Selling' | abpLocalization }}</label>
            </div>
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="buying" [(ngModel)]="form.buying" />
              <label class="form-check-label" for="buying">{{ 'Buying' | abpLocalization }}</label>
            </div>
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="disabled" [(ngModel)]="form.isDisabled" />
              <label class="form-check-label" for="disabled">{{ 'IsDisabled' | abpLocalization }}</label>
            </div>
          </div>
        </div>

        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ 'ValidFrom' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.validFrom" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ValidUpto' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.validUpto" />
          </div>
          <div class="col-md-3 d-flex align-items-end gap-3">
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="mixed" [(ngModel)]="form.mixedConditions" />
              <label class="form-check-label" for="mixed">{{ 'MixedConditions' | abpLocalization }}</label>
            </div>
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="cumulative" [(ngModel)]="form.isCumulative" />
              <label class="form-check-label" for="cumulative">{{ 'IsCumulative' | abpLocalization }}</label>
            </div>
          </div>
        </div>

        @if (form.applyOn !== applyOnTransaction) {
          <h6 class="mb-2">{{ 'Targets' | abpLocalization }} ({{ form.applyOn === applyOnItemGroup ? ('ItemGroup' | abpLocalization) : ('Item' | abpLocalization) }})</h6>
          <div class="d-flex flex-wrap gap-2 mb-2">
            @for (t of form.targets; track $index) {
              <span class="badge bg-light text-dark border d-flex align-items-center gap-1">
                {{ t.targetName }}
                <i class="fa fa-times cursor-pointer" (click)="form.targets.splice($index,1)"></i>
              </span>
            }
          </div>
          <select class="form-select form-select-sm mb-3" style="max-width:400px" (change)="addTarget($any($event.target).value); $any($event.target).value=''">
            <option value="">-- {{ 'AddTarget' | abpLocalization }} --</option>
            @for (i of targetOptions(); track i.id) {
              <option [value]="i.id">{{ i.name }}</option>
            }
          </select>
        }

        @if (form.applicableFor !== applicableForNone) {
          <h6 class="mb-2">{{ 'Parties' | abpLocalization }}</h6>
          <div class="d-flex flex-wrap gap-2 mb-2">
            @for (p of form.parties; track $index) {
              <span class="badge bg-light text-dark border d-flex align-items-center gap-1">
                {{ p.partyName }}
                <i class="fa fa-times cursor-pointer" (click)="form.parties.splice($index,1)"></i>
              </span>
            }
          </div>
          <select class="form-select form-select-sm mb-3" style="max-width:400px" (change)="addParty($any($event.target).value); $any($event.target).value=''">
            <option value="">-- {{ 'AddParty' | abpLocalization }} --</option>
            @for (p of partyOptions(); track p.id) {
              <option [value]="p.id">{{ p.name }}</option>
            }
          </select>
        }

        <h6 class="mb-2">{{ 'PriceDiscountSlabs' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr>
            <th>{{ 'Type' | abpLocalization }}</th><th>{{ 'DiscountPercentage' | abpLocalization }}</th>
            <th>{{ 'DiscountAmount' | abpLocalization }}</th><th>{{ 'Rate' | abpLocalization }}</th>
            <th>{{ 'MinQty' | abpLocalization }}</th><th>{{ 'MaxQty' | abpLocalization }}</th>
            <th>{{ 'Priority' | abpLocalization }}</th><th></th>
          </tr></thead>
          <tbody>
            @for (s of form.priceDiscountSlabs; track $index) {
              <tr>
                <td>
                  <select class="form-select form-select-sm" [(ngModel)]="s.rateOrDiscount">
                    @for (o of rateOrDiscountOptions; track o.value) { <option [ngValue]="o.value">{{ o.key }}</option> }
                  </select>
                </td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.discountPercentage" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.discountAmount" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.rate" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.minQty" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.maxQty" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.priority" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.priceDiscountSlabs.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addPriceSlab()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <h6 class="mb-2">{{ 'ProductDiscountSlabs' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr>
            <th>{{ 'FreeItem' | abpLocalization }}</th><th>{{ 'SameItem' | abpLocalization }}</th>
            <th>{{ 'FreeQty' | abpLocalization }}</th><th>{{ 'MinQty' | abpLocalization }}</th>
            <th>{{ 'MinAmount' | abpLocalization }}</th><th>{{ 'IsRecursive' | abpLocalization }}</th>
            <th>{{ 'RecurseFor' | abpLocalization }}</th><th></th>
          </tr></thead>
          <tbody>
            @for (s of form.productDiscountSlabs; track $index) {
              <tr>
                <td>
                  @if (!s.sameItem) {
                    <select class="form-select form-select-sm" [(ngModel)]="s.freeItemId">
                      <option value="">--</option>
                      @for (i of availableItems(); track i.id) { <option [value]="i.id">{{ i.itemCode }}</option> }
                    </select>
                  } @else { <span class="text-muted">{{ 'SameItem' | abpLocalization }}</span> }
                </td>
                <td class="text-center"><input type="checkbox" [(ngModel)]="s.sameItem" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.freeQty" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.minQty" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.minAmount" /></td>
                <td class="text-center"><input type="checkbox" [(ngModel)]="s.isRecursive" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="s.recurseFor" [disabled]="!s.isRecursive" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.productDiscountSlabs.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addProductSlab()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/sales/promotional-schemes">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving()"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class PromotionalSchemeFormComponent implements OnInit {
  private service = inject(PromotionalSchemeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private itemGroupService = inject(ItemGroupService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);

  applyOnOptions = pricingRuleApplyOnOptions;
  applicableForOptions = promotionalSchemeApplicableForOptions;
  rateOrDiscountOptions = pricingRuleTypeOptions.filter(o => o.value === PricingRuleType.Discount || o.value === PricingRuleType.Rate);
  applyOnTransaction = PricingRuleApplyOn.TransactionTotal;
  applyOnItemGroup = PricingRuleApplyOn.ItemGroup;
  applicableForNone = PromotionalSchemeApplicableFor.None;

  saving = signal(false);
  isEdit = signal(false);
  private schemeId: string | null = null;

  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  itemGroups = signal<{ id: string; name: string }[]>([]);
  customers = signal<{ id: string; name: string }[]>([]);
  suppliers = signal<{ id: string; name: string }[]>([]);

  form: any = {
    title: '', isDisabled: false, applyOn: PricingRuleApplyOn.ItemCode,
    mixedConditions: false, isCumulative: false, applyRuleOnOtherItem: false,
    selling: true, buying: false, applicableFor: PromotionalSchemeApplicableFor.None,
    validFrom: null, validUpto: null,
    targets: [] as { targetId: string; targetName: string }[],
    parties: [] as { partyId: string; partyName: string }[],
    priceDiscountSlabs: [] as any[],
    productDiscountSlabs: [] as any[],
  };

  ngOnInit(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName }))));
    this.itemGroupService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.itemGroups.set((r.items ?? []).map((g: any) => ({ id: g.id, name: g.itemGroupName ?? g.name }))));
    this.customerService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.customers.set((r.items ?? []).map((c: any) => ({ id: c.id, name: c.customerName ?? c.name }))));
    this.supplierService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.suppliers.set((r.items ?? []).map((s: any) => ({ id: s.id, name: s.supplierName ?? s.name }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.schemeId = id;
      this.service.get(id).subscribe(s => {
        this.form = {
          title: s.title, isDisabled: s.isDisabled, applyOn: s.applyOn,
          mixedConditions: s.mixedConditions, isCumulative: s.isCumulative,
          applyRuleOnOtherItem: s.applyRuleOnOtherItem, otherApplyOn: s.otherApplyOn, otherTargetId: s.otherTargetId,
          selling: s.selling, buying: s.buying, applicableFor: s.applicableFor,
          validFrom: s.validFrom ? s.validFrom.substring(0, 10) : null,
          validUpto: s.validUpto ? s.validUpto.substring(0, 10) : null,
          currencyId: s.currencyId,
          targets: (s.targets ?? []).map(t => ({ targetId: t.targetId, targetName: t.targetName })),
          parties: (s.parties ?? []).map(p => ({ partyId: p.partyId, partyName: p.partyName })),
          priceDiscountSlabs: s.priceDiscountSlabs ?? [],
          productDiscountSlabs: s.productDiscountSlabs ?? [],
        };
      });
    }
  }

  targetOptions(): { id: string; name: string }[] {
    return this.form.applyOn === this.applyOnItemGroup
      ? this.itemGroups().filter(g => !this.form.targets.some((t: any) => t.targetId === g.id))
      : this.availableItems().map(i => ({ id: i.id, name: `${i.itemCode} — ${i.itemName}` }))
          .filter(i => !this.form.targets.some((t: any) => t.targetId === i.id));
  }

  partyOptions(): { id: string; name: string }[] {
    const list = this.form.applicableFor === PromotionalSchemeApplicableFor.Supplier || this.form.applicableFor === PromotionalSchemeApplicableFor.SupplierGroup
      ? this.suppliers() : this.customers();
    return list.filter(p => !this.form.parties.some((x: any) => x.partyId === p.id));
  }

  addTarget(id: string): void {
    if (!id) return;
    const source = this.form.applyOn === this.applyOnItemGroup ? this.itemGroups() : this.targetOptions();
    const found = source.find((x: any) => x.id === id);
    const name = found ? ('name' in found ? found.name : `${(found as any).itemCode} — ${(found as any).itemName}`) : id;
    this.form.targets.push({ targetId: id, targetName: name });
  }

  addParty(id: string): void {
    if (!id) return;
    const found = this.partyOptions().find(p => p.id === id);
    this.form.parties.push({ partyId: id, partyName: found?.name ?? id });
  }

  addPriceSlab(): void {
    this.form.priceDiscountSlabs.push({
      rateOrDiscount: PricingRuleType.Discount, discountPercentage: 0, discountAmount: 0, rate: 0,
      minQty: 0, maxQty: 0, minAmount: 0, maxAmount: 0, priority: 1, isDisabled: false,
    });
  }

  addProductSlab(): void {
    this.form.productDiscountSlabs.push({
      freeItemId: '', freeQty: 1, freeItemRate: 0, sameItem: false,
      minQty: 0, maxQty: 0, minAmount: 0, maxAmount: 0, priority: 1,
      isRecursive: false, recurseFor: 0, roundFreeQty: false,
    });
  }

  save(): void {
    this.saving.set(true);
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      title: this.form.title,
      isDisabled: this.form.isDisabled,
      applyOn: this.form.applyOn,
      mixedConditions: this.form.mixedConditions,
      isCumulative: this.form.isCumulative,
      applyRuleOnOtherItem: this.form.applyRuleOnOtherItem,
      otherApplyOn: this.form.otherApplyOn ?? null,
      otherTargetId: this.form.otherTargetId ?? null,
      selling: this.form.selling,
      buying: this.form.buying,
      applicableFor: this.form.applicableFor,
      validFrom: this.form.validFrom || null,
      validUpto: this.form.validUpto || null,
      currencyId: this.form.currencyId ?? null,
      targets: this.form.targets,
      parties: this.form.parties,
      priceDiscountSlabs: this.form.priceDiscountSlabs.filter((s: any) => !s.sameItem || true),
      productDiscountSlabs: this.form.productDiscountSlabs.filter((s: any) => s.sameItem || s.freeItemId),
    };

    const req = this.schemeId ? this.service.update(this.schemeId, dto) : this.service.create(dto);
    req.subscribe({
      next: () => {
        this.toaster.success(this.schemeId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        this.router.navigate(['/sales/promotional-schemes']);
      },
      error: () => this.saving.set(false),
    });
  }
}
