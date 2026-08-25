import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LetterHeadService } from '../../proxy/core/letter-head.service';
import { LetterHeadDto } from '../../proxy/core/models';
import { LetterHeadFor } from '../../proxy/core/letter-head-for.enum';

@Component({
  selector: 'app-letter-head-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title mb-0">Letter Heads</h5>
        <a routerLink="/settings/letter-heads/new" class="btn btn-primary btn-sm">New Letter Head</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered">
          <thead>
            <tr>
              <th>Name</th>
              <th>For</th>
              <th>Default</th>
              <th>Disabled</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (letterHead of letterHeads; track letterHead.id) {
              <tr>
                <td><a [routerLink]="['/settings/letter-heads', letterHead.id]">{{ letterHead.letterHeadName }}</a></td>
                <td>{{ letterHead.letterHeadFor === forDocType ? 'DocType' : 'Report' }}</td>
                <td>
                  <span class="badge" [ngClass]="letterHead.isDefault ? 'bg-success' : 'bg-secondary'">
                    {{ letterHead.isDefault ? 'Yes' : 'No' }}
                  </span>
                </td>
                <td>
                  @if (letterHead.isDisabled) { <span class="badge bg-warning text-dark">Disabled</span> }
                </td>
                <td>
                  @if (!letterHead.isDefault) {
                    <button class="btn btn-sm btn-outline-success" (click)="setDefault(letterHead)">Set Default</button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class LetterHeadListComponent implements OnInit {
  private service = inject(LetterHeadService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  letterHeads: LetterHeadDto[] = [];
  forDocType = LetterHeadFor.DocType;

  ngOnInit() {
    this.load();
  }

  load(): void {
    this.service.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe(res => {
      this.letterHeads = res.items;
    });
  }

  setDefault(letterHead: LetterHeadDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.setDefault(letterHead.id!).subscribe({
        next: () => { this.toaster.success('::SuccessfullySaved'); this.load(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    });
  }
}
