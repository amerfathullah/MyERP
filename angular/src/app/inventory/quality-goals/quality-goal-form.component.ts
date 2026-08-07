import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';

@Component({
  selector: 'app-quality-goal-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title">{{ isEdit ? 'Edit' : 'New' }} Quality Goal</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">Name *</label>
            <input type="text" class="form-control" formControlName="name">
          </div>
          <div class="mb-3">
            <label class="form-label">Goal Description</label>
            <textarea class="form-control" formControlName="goal"></textarea>
          </div>
          <div class="mb-3">
            <label class="form-label">Frequency *</label>
            <select class="form-select" formControlName="frequency">
              <option value="Daily">Daily</option>
              <option value="Weekly">Weekly</option>
              <option value="Monthly">Monthly</option>
              <option value="Quarterly">Quarterly</option>
            </select>
          </div>
          <div class="mb-3">
            <label class="form-label">Target Value *</label>
            <input type="number" class="form-control" formControlName="targetValue">
          </div>
          <div class="mb-3">
            <label class="form-label">Unit of Measure (UOM)</label>
            <input type="text" class="form-control" formControlName="uom">
          </div>
          <div class="mb-3 form-check">
            <input type="checkbox" class="form-check-input" formControlName="isEnabled" id="isEnabled">
            <label class="form-check-label" for="isEnabled">Is Enabled</label>
          </div>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
          <a routerLink="/inventory/quality-goals" class="btn btn-secondary ms-2">Cancel</a>
        </form>
      </div>
    </div>
  `
})
export class QualityGoalFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(QualityManagementService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      name: ['', Validators.required],
      goal: [''],
      frequency: ['Monthly', Validators.required],
      targetValue: [0, Validators.required],
      uom: [''],
      isEnabled: [true]
    });
  }

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.getGoal(this.id).subscribe(res => {
        this.form.patchValue(res);
      });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit 
      ? this.service.updateGoal(this.id!, this.form.value)
      : this.service.createGoal(this.form.value);
    
    req.subscribe(() => {
      this.router.navigate(['/inventory/quality-goals']);
    });
  }
}
