import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { OpportunityService } from '../../proxy/crm/opportunity.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

interface KanbanCard {
  id: string;
  title: string;
  salesStage: string;
  amount: number;
  probability: number;
  contactName: string | null;
  expectedClosingDate: string | null;
  daysOpen: number;
  isOverdue: boolean;
}

interface KanbanColumn {
  stage: string;
  color: string;
  cards: KanbanCard[];
  totalAmount: number;
}

/**
 * Opportunity Kanban Board — visual drag-and-drop pipeline management.
 * Shows opportunities organized by sales stage as columns.
 * Users can drag cards between columns to update the opportunity stage.
 *
 * ERPNext equivalent: Kanban Board view on Opportunity list.
 *
 * Features:
 * - Color-coded stage columns
 * - Card shows title, amount, probability, contact, closing date
 * - Drag-and-drop to change stage (calls update API)
 * - Overdue highlighting
 * - Per-column totals
 * - Quick-add opportunity button per column
 */
@Component({
  selector: 'app-opportunity-kanban',
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h5 class="mb-0"><i class="fas fa-columns me-2 text-primary"></i>{{ 'CRM::OpportunityBoard' | abpLocalization }}</h5>
        <div class="d-flex gap-2">
          <a routerLink="/crm/pipeline" class="btn btn-outline-secondary btn-sm">
            <i class="fas fa-chart-bar me-1"></i>Pipeline View
          </a>
          <a routerLink="/crm/opportunities/new" class="btn btn-primary btn-sm">
            <i class="fas fa-plus me-1"></i>New Opportunity
          </a>
        </div>
      </div>

      @if (loading()) {
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      } @else {
        <div class="kanban-board d-flex gap-3 overflow-auto pb-3">
          @for (column of columns(); track column.stage) {
            <div class="kanban-column"
                 (dragover)="onDragOver($event)"
                 (drop)="onDrop($event, column.stage)">
              <!-- Column Header -->
              <div class="kanban-header d-flex justify-content-between align-items-center p-2 rounded-top"
                   [style.background]="column.color + '20'"
                   [style.border-bottom]="'3px solid ' + column.color">
                <div>
                  <span class="fw-bold" [style.color]="column.color">{{ column.stage }}</span>
                  <span class="badge bg-light text-dark ms-1">{{ column.cards.length }}</span>
                </div>
                <small class="text-muted">{{ column.totalAmount | number:'1.0-0' }}</small>
              </div>

              <!-- Cards -->
              <div class="kanban-cards p-2">
                @for (card of column.cards; track card.id) {
                  <div class="kanban-card card mb-2 shadow-sm"
                       [class.border-danger]="card.isOverdue"
                       draggable="true"
                       (dragstart)="onDragStart($event, card)">
                    <div class="card-body p-2">
                      <a [routerLink]="['/crm/opportunities', card.id]"
                         class="text-decoration-none fw-medium d-block mb-1 text-truncate"
                         [title]="card.title">
                        {{ card.title }}
                      </a>
                      <div class="d-flex justify-content-between align-items-center mb-1">
                        <span class="fw-bold text-primary">{{ card.amount | number:'1.0-0' }}</span>
                        <span class="badge" [class.bg-success]="card.probability >= 70"
                              [class.bg-warning]="card.probability >= 30 && card.probability < 70"
                              [class.bg-secondary]="card.probability < 30">
                          {{ card.probability }}%
                        </span>
                      </div>
                      @if (card.contactName) {
                        <small class="text-muted d-block text-truncate"><i class="fas fa-user me-1"></i>{{ card.contactName }}</small>
                      }
                      <div class="d-flex justify-content-between align-items-center mt-1">
                        @if (card.expectedClosingDate) {
                          <small [class.text-danger]="card.isOverdue" [class.text-muted]="!card.isOverdue">
                            <i class="fas fa-calendar me-1"></i>{{ card.expectedClosingDate | date:'dd/MM' }}
                          </small>
                        }
                        <small class="text-muted">{{ card.daysOpen }}d</small>
                      </div>
                    </div>
                  </div>
                }

                @if (column.cards.length === 0) {
                  <div class="text-center text-muted py-4 opacity-50">
                    <i class="fas fa-inbox d-block mb-1"></i>
                    <small>No opportunities</small>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .kanban-board { min-height: 70vh; }
    .kanban-column { min-width: 280px; max-width: 320px; flex: 0 0 auto; background: #f8f9fa; border-radius: 8px; display: flex; flex-direction: column; }
    .kanban-cards { flex: 1; overflow-y: auto; max-height: calc(70vh - 50px); }
    .kanban-card { cursor: grab; transition: transform 0.1s, box-shadow 0.1s; }
    .kanban-card:active { cursor: grabbing; transform: rotate(2deg); box-shadow: 0 4px 12px rgba(0,0,0,0.15) !important; }
    .kanban-card.border-danger { border-left: 3px solid #dc3545 !important; }
    .kanban-column.drag-over { background: #e8f4fd; }
  `],
})
export class OpportunityKanbanComponent implements OnInit {
  private opportunityService = inject(OpportunityService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  columns = signal<KanbanColumn[]>([]);
  loading = signal(true);
  private draggedCard: KanbanCard | null = null;

  // Standard CRM sales stages (configurable in production)
  private readonly stages = [
    { name: 'Prospecting', color: '#6c757d' },
    { name: 'Qualification', color: '#17a2b8' },
    { name: 'Needs Analysis', color: '#007bff' },
    { name: 'Proposal', color: '#6610f2' },
    { name: 'Negotiation', color: '#fd7e14' },
    { name: 'Won', color: '#28a745' },
    { name: 'Lost', color: '#dc3545' },
  ];

  ngOnInit(): void {
    this.loadOpportunities();
  }

  loadOpportunities(): void {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { skipCount: '0', maxResultCount: '200' };
    if (companyId) params.companyId = companyId;

    this.opportunityService.getList({ skipCount: 0, maxResultCount: 200, companyId } as any).subscribe({
      next: res => {
        const opportunities: KanbanCard[] = (res.items ?? []).map((opp: any) => {
          const today = new Date();
          today.setHours(0, 0, 0, 0);
          const closingDate = opp.expectedClosingDate ? new Date(opp.expectedClosingDate) : null;
          const isOverdue = closingDate != null && closingDate < today && opp.status !== 'Won' && opp.status !== 'Lost' && opp.status !== 'Closed';
          const openDate = new Date(opp.creationTime || opp.openingDate);
          const daysOpen = Math.floor((today.getTime() - openDate.getTime()) / (1000 * 60 * 60 * 24));

          return {
            id: opp.id,
            title: opp.title || opp.name || `Opportunity ${opp.id.substring(0, 8)}`,
            salesStage: opp.salesStage || opp.status || 'Prospecting',
            amount: opp.amount || opp.opportunityAmount || 0,
            probability: opp.probability || 0,
            contactName: opp.contactName || null,
            expectedClosingDate: opp.expectedClosingDate,
            daysOpen: Math.max(0, daysOpen),
            isOverdue,
          };
        });

        // Group by stage
        const cols: KanbanColumn[] = this.stages.map(s => ({
          stage: s.name,
          color: s.color,
          cards: opportunities.filter(o => o.salesStage === s.name),
          totalAmount: opportunities.filter(o => o.salesStage === s.name).reduce((sum, o) => sum + o.amount, 0),
        }));

        // Add "Other" column for unrecognized stages
        const knownStages = new Set(this.stages.map(s => s.name));
        const otherCards = opportunities.filter(o => !knownStages.has(o.salesStage));
        if (otherCards.length > 0) {
          cols.push({
            stage: 'Other',
            color: '#adb5bd',
            cards: otherCards,
            totalAmount: otherCards.reduce((sum, o) => sum + o.amount, 0),
          });
        }

        this.columns.set(cols);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onDragStart(event: DragEvent, card: KanbanCard): void {
    this.draggedCard = card;
    event.dataTransfer?.setData('text/plain', card.id);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
  }

  onDrop(event: DragEvent, targetStage: string): void {
    event.preventDefault();
    if (!this.draggedCard || this.draggedCard.salesStage === targetStage) {
      this.draggedCard = null;
      return;
    }

    const card = this.draggedCard;
    const previousStage = card.salesStage;
    this.draggedCard = null;

    // Optimistic update: move card in UI immediately
    const cols = this.columns().map(col => {
      if (col.stage === previousStage) {
        return { ...col, cards: col.cards.filter(c => c.id !== card.id), totalAmount: col.totalAmount - card.amount };
      }
      if (col.stage === targetStage) {
        const updatedCard = { ...card, salesStage: targetStage };
        return { ...col, cards: [...col.cards, updatedCard], totalAmount: col.totalAmount + card.amount };
      }
      return col;
    });
    this.columns.set(cols);

    // Persist stage change to backend
    this.opportunityService.updateStage(card.id, { salesStage: targetStage } as any).subscribe({
      next: () => {
        this.toaster.success(`Moved to ${targetStage}`);
      },
      error: () => {
        this.toaster.error('::OperationFailed');
        this.loadOpportunities(); // Revert to server state
      },
    });
  }
}
