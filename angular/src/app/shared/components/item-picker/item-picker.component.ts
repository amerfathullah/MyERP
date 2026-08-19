import { Component, ElementRef, EventEmitter, forwardRef, inject, Input, Output, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Subject, debounceTime } from 'rxjs';
import { ItemService } from '../../../proxy/inventory/item.service';
import type { ItemDto } from '../../../proxy/inventory/models';

/**
 * Reusable Item search-select. Plugs into reactive forms via formControlName like a native
 * input (implements ControlValueAccessor) — the underlying value is the Item's Guid id.
 *
 * Existing forms (BOM/Sales Order/Purchase Order/Stock Entry) preload up to 500 items into a
 * plain <select>, which silently hides items beyond that cap and never uses ItemService's
 * server-side `filter` param. This component debounce-searches server-side instead, the same
 * pattern already proven in pos.component.ts.
 */
@Component({
  selector: 'app-item-picker',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => ItemPickerComponent),
      multi: true,
    },
  ],
  template: `
    <div class="position-relative">
      <input
        #inputEl
        type="text"
        class="form-control"
        [class.is-invalid]="invalid"
        [placeholder]="placeholder"
        [disabled]="disabled"
        [(ngModel)]="searchText"
        (ngModelChange)="onSearchChange($event)"
        (focus)="onFocus()"
        (blur)="onBlur()"
      />
      @if (showDropdown() && results().length > 0) {
        <div class="list-group position-absolute w-100 shadow-sm" style="z-index:1050; max-height:260px; overflow-y:auto;">
          @for (item of results(); track item.id) {
            <button type="button" class="list-group-item list-group-item-action py-1 px-2"
              (mousedown)="selectItem(item)">
              <span class="fw-medium">{{ item.itemCode }}</span>
              <span class="text-muted"> — {{ item.itemName }}</span>
            </button>
          }
        </div>
      }
    </div>
  `,
})
export class ItemPickerComponent implements ControlValueAccessor {
  private itemService = inject(ItemService);

  @Input() placeholder = 'Search item by code or name...';
  @Input() companyId: string | null = null;
  @Input() invalid = false;

  /** Emits the full selected ItemDto (or null when cleared) — use for auto-filling related fields (uom, rate, etc). */
  @Output() itemSelected = new EventEmitter<ItemDto | null>();

  @ViewChild('inputEl') inputEl!: ElementRef<HTMLInputElement>;

  searchText = '';
  results = signal<ItemDto[]>([]);
  showDropdown = signal(false);
  disabled = false;

  private valueId: string | null = null;
  private searchSubject = new Subject<string>();
  private onChange: (value: string | null) => void = () => {};
  private onTouched: () => void = () => {};

  constructor() {
    this.searchSubject.pipe(debounceTime(300)).subscribe((query) => {
      if (!query || query.length < 1) {
        this.results.set([]);
        return;
      }
      this.itemService.getList({
        filter: query,
        companyId: this.companyId,
        skipCount: 0,
        maxResultCount: 20,
        sorting: '',
      } as any).subscribe((res) => this.results.set(res.items ?? []));
    });
  }

  onSearchChange(query: string): void {
    this.showDropdown.set(true);
    this.searchSubject.next(query);
    if (!query) {
      this.valueId = null;
      this.onChange(null);
      this.itemSelected.emit(null);
    }
  }

  onFocus(): void {
    if (this.searchText) this.showDropdown.set(true);
  }

  onBlur(): void {
    // Delay so the (mousedown) selection on a dropdown item fires before the dropdown closes.
    setTimeout(() => this.showDropdown.set(false), 150);
    this.onTouched();
  }

  selectItem(item: ItemDto): void {
    this.valueId = item.id ?? null;
    this.searchText = `${item.itemCode} — ${item.itemName}`;
    this.showDropdown.set(false);
    this.onChange(this.valueId);
    this.itemSelected.emit(item);
  }

  writeValue(value: string | null): void {
    this.valueId = value;
    if (!value) {
      this.searchText = '';
      return;
    }
    this.itemService.get(value).subscribe({
      next: (item) => { this.searchText = `${item.itemCode} — ${item.itemName}`; },
      error: () => { this.searchText = ''; },
    });
  }

  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }
}
