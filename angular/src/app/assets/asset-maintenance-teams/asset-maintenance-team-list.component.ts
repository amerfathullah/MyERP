import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { AssetMaintenanceTeamService } from '../../proxy/assets/asset-maintenance-team.service';
import type { AssetMaintenanceTeamDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-maintenance-team-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'AssetMaintenanceTeams' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/assets/maintenance-teams/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewAssetMaintenanceTeam' | abpLocalization }}
        </button>
      </div>

      @if (teams.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-people-group fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoAssetMaintenanceTeamsYet' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'TeamName' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Members' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (t of teams; track t.id) {
                  <tr>
                    <td>{{ t.teamName }}</td>
                    <td class="text-end">{{ t.members?.length ?? 0 }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/assets/maintenance-teams', t.id]">
                          <i class="fa fa-pencil"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="remove(t)"><i class="fa fa-trash"></i></button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class AssetMaintenanceTeamListComponent implements OnInit {
  private service = inject(AssetMaintenanceTeamService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  teams: AssetMaintenanceTeamDto[] = [];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.service.getList({ maxResultCount: 200 }).subscribe(r => this.teams = r.items ?? []);
  }

  remove(t: AssetMaintenanceTeamDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(t.id!).subscribe(() => { this.toaster.success('::SuccessfullyDeleted'); this.load(); });
    });
  }
}
