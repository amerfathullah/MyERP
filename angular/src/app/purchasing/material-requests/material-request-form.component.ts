import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { MaterialRequestStore } from '../store/material-request.store';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-material-request-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './material-request-form.component.html',
  styleUrls: ['./material-request-form.component.scss'],
})
export class MaterialRequestFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private store = inject(MaterialRequestStore);
  private companyService = inject(CompanyService);

  form!: FormGroup;
  companies = signal<CompanyDto[]>([]);

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      requestType: [0, Validators.required],
      requestDate: [new Date().toISOString().split('T')[0], Validators.required],
      requiredByDate: [''],
      sourceWarehouseId: [''],
      targetWarehouseId: [''],
      notes: [''],
      items: this.fb.array([]),
    });
    this.addItemRow();

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe((res) => this.companies.set(res.items ?? []));
  }

  addItemRow(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      itemName: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
      uom: ['Unit'],
      warehouseId: [''],
    }));
  }

  removeItemRow(index: number): void {
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.store.create(this.form.getRawValue());
    this.router.navigate(['/purchasing/material-requests']);
  }

  cancel(): void {
    this.router.navigate(['/purchasing/material-requests']);
  }
}
