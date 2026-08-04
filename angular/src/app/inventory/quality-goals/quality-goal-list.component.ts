import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/common';
import { QualityManagementService } from '@proxy/inventory/quality-management.service';
import { QualityGoalDto } from '@proxy/inventory/models';

@Component({
  selector: 'app-quality-goal-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title">Quality Goals</h5>
        <a routerLink="/inventory/quality-goals/new" class="btn btn-primary btn-sm">New Goal</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered">
          <thead>
            <tr>
              <th>Name</th>
              <th>Frequency</th>
              <th>Target Value</th>
              <th>Enabled</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let goal of goals">
              <td>
                <a [routerLink]="['/inventory/quality-goals', goal.id]">{{ goal.name }}</a>
              </td>
              <td>{{ goal.frequency }}</td>
              <td>{{ goal.targetValue }} {{ goal.uom }}</td>
              <td>
                <span class="badge" [ngClass]="goal.isEnabled ? 'bg-success' : 'bg-secondary'">
                  {{ goal.isEnabled ? 'Yes' : 'No' }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class QualityGoalListComponent implements OnInit {
  private service = inject(QualityManagementService);
  goals: QualityGoalDto[] = [];

  ngOnInit() {
    this.service.getGoalList({ maxResultCount: 100, skipCount: 0 }).subscribe(res => {
      this.goals = res.items;
    });
  }
}
