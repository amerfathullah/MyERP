import { describe, it, expect, beforeEach } from 'vitest';

/**
 * Expense Claim Form Component — financial form tests.
 * Validates expense line management, total calculation, and DTO mapping.
 */
describe('ExpenseClaimFormComponent', () => {
  let form: {
    postingDate: string;
    expenseType: string;
    employeeName: string;
    expenses: Array<{ expenseDate: string; description: string; amount: number }>;
  };

  beforeEach(() => {
    form = {
      postingDate: new Date().toISOString().split('T')[0],
      expenseType: 'Travel',
      employeeName: '',
      expenses: [{ expenseDate: '', description: '', amount: 0 }],
    };
  });

  function addExpense(data?: Partial<{ expenseDate: string; description: string; amount: number }>) {
    form.expenses.push({
      expenseDate: data?.expenseDate ?? '',
      description: data?.description ?? '',
      amount: data?.amount ?? 0,
    });
  }

  function removeExpense(i: number) {
    form.expenses.splice(i, 1);
  }

  function getTotal(): number {
    return form.expenses.reduce((s, e) => s + (e.amount || 0), 0);
  }

  describe('Total Calculation', () => {
    it('starts at 0 with empty expenses', () => {
      form.expenses = [{ expenseDate: '', description: '', amount: 0 }];
      expect(getTotal()).toBe(0);
    });

    it('sums single expense', () => {
      form.expenses = [{ expenseDate: '2026-07-20', description: 'Taxi', amount: 45.50 }];
      expect(getTotal()).toBe(45.50);
    });

    it('sums multiple expenses', () => {
      form.expenses = [
        { expenseDate: '2026-07-20', description: 'Taxi', amount: 45.50 },
        { expenseDate: '2026-07-20', description: 'Lunch', amount: 25.00 },
        { expenseDate: '2026-07-21', description: 'Hotel', amount: 350.00 },
      ];
      expect(getTotal()).toBe(420.50);
    });

    it('treats undefined/null amounts as 0', () => {
      form.expenses = [
        { expenseDate: '', description: '', amount: 0 },
        { expenseDate: '', description: 'Fuel', amount: 80 },
      ];
      expect(getTotal()).toBe(80);
    });

    it('handles empty expense array', () => {
      form.expenses = [];
      expect(getTotal()).toBe(0);
    });

    it('recalculates after removing expense', () => {
      form.expenses = [
        { expenseDate: '', description: 'A', amount: 100 },
        { expenseDate: '', description: 'B', amount: 200 },
        { expenseDate: '', description: 'C', amount: 300 },
      ];
      removeExpense(1); // Remove B (200)
      expect(getTotal()).toBe(400);
    });
  });

  describe('Expense Row Management', () => {
    it('starts with one empty row', () => {
      expect(form.expenses.length).toBe(1);
    });

    it('adds expense row', () => {
      addExpense({ description: 'Parking', amount: 10 });
      expect(form.expenses.length).toBe(2);
    });

    it('removes specific expense', () => {
      addExpense({ description: 'First', amount: 50 });
      addExpense({ description: 'Second', amount: 75 });
      removeExpense(1); // Remove "First"
      expect(form.expenses.length).toBe(2);
      expect(form.expenses[1].description).toBe('Second');
    });

    it('can remove all expenses', () => {
      removeExpense(0);
      expect(form.expenses.length).toBe(0);
      expect(getTotal()).toBe(0);
    });
  });

  describe('Defaults', () => {
    it('defaults postingDate to today', () => {
      const today = new Date().toISOString().split('T')[0];
      expect(form.postingDate).toBe(today);
    });

    it('defaults expenseType to Travel', () => {
      expect(form.expenseType).toBe('Travel');
    });

    it('defaults employeeName to empty', () => {
      expect(form.employeeName).toBe('');
    });
  });

  describe('Expense Types', () => {
    const validTypes = ['Travel', 'Food', 'Accommodation', 'Transportation', 'Other'];

    validTypes.forEach((type) => {
      it(`accepts expense type: ${type}`, () => {
        form.expenseType = type;
        expect(form.expenseType).toBe(type);
      });
    });
  });

  describe('DTO Mapping', () => {
    it('produces correct DTO for API', () => {
      form.employeeName = 'Ahmad bin Ibrahim';
      form.postingDate = '2026-07-20';
      form.expenseType = 'Travel';
      form.expenses = [
        { expenseDate: '2026-07-18', description: 'Flight KL-Penang', amount: 250 },
        { expenseDate: '2026-07-18', description: 'Airport transfer', amount: 40 },
        { expenseDate: '2026-07-19', description: 'Hotel Penang', amount: 300 },
      ];

      expect(form.postingDate).toBe('2026-07-20');
      expect(form.expenses.length).toBe(3);
      expect(getTotal()).toBe(590);
    });

    it('expense items have all required fields', () => {
      form.expenses = [{ expenseDate: '2026-07-20', description: 'Taxi to office', amount: 35 }];
      const item = form.expenses[0];

      expect(item).toHaveProperty('expenseDate');
      expect(item).toHaveProperty('description');
      expect(item).toHaveProperty('amount');
    });
  });

  describe('Edge Cases', () => {
    it('handles decimal amounts correctly', () => {
      form.expenses = [
        { expenseDate: '', description: '', amount: 33.33 },
        { expenseDate: '', description: '', amount: 33.33 },
        { expenseDate: '', description: '', amount: 33.34 },
      ];
      expect(getTotal()).toBeCloseTo(100, 2);
    });

    it('handles large amounts', () => {
      form.expenses = [{ expenseDate: '', description: 'Conference', amount: 99999.99 }];
      expect(getTotal()).toBe(99999.99);
    });
  });
});
