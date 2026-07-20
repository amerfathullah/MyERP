import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { OpportunityStore } from '../store/opportunity.store';
import { OpportunityService } from '../../proxy/crm/opportunity.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-opportunity-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './opportunity-form.component.html',
  styleUrls: ['./opportunity-form.component.scss'],
})
export class OpportunityFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(OpportunityStore);
  private service = inject(OpportunityService);
  private companyContext = inject(CompanyContextService);

  form!: FormGroup;
  isEditMode = false;
  entityId: string | null = null;

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      opportunityType: [0],
      contactName: [''],
      contactEmail: ['', [Validators.email]],
      contactPhone: [''],
      salesStage: ['Prospecting'],
      probability: [20, [Validators.min(0), Validators.max(100)]],
      expectedClosingDate: [''],
      opportunityAmount: [0, [Validators.min(0)]],
      currencyCode: ['MYR'],
      territory: [''],
      companyId: ['', Validators.required],
      notes: [''],
      items: this.fb.array([]),
    });

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((opp) => {
        this.form.patchValue(opp);
        opp.items?.forEach((item: any) => this.addItemRow(item));
      });
    } else {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }
  }

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  addItemRow(item?: any): void {
    this.items.push(this.fb.group({
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      uom: [item?.uom ?? 'EA'],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();

    if (this.isEditMode) {
      this.service.update(this.entityId!, value as any).subscribe({
        next: () => this.router.navigate(['/crm/opportunities']),
        error: () => {},
      });
    } else {
      this.service.create(value as any).subscribe({
        next: () => this.router.navigate(['/crm/opportunities']),
        error: () => {},
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}