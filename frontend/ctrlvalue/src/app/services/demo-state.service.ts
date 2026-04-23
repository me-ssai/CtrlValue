import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import {
    AccountDto,
    TransactionDto,
    CategoryDto,
    BudgetDto,
    PositionDto,
    InstrumentDto,
    EntityDto
} from './api.generated';

export interface DemoBootstrapDto {
    entity: EntityDto;
    accounts: AccountDto[];
    recentTransactions: TransactionDto[];
    categories: CategoryDto[];
    budgets: BudgetDto[];
    positions: PositionDto[];
    instruments: InstrumentDto[];
}

@Injectable({ providedIn: 'root' })
export class DemoStateService {
    private http = inject(HttpClient);

    readonly isDemoMode = environment.demo;

    private _baseline: DemoBootstrapDto | null = null;
    private _baselineSubject = new BehaviorSubject<DemoBootstrapDto | null>(null);
    baseline$ = this._baselineSubject.asObservable();

    // Session overlay maps — delta from seeded baseline
    private _accounts    = new Map<string, AccountDto>();
    private _transactions= new Map<string, TransactionDto>();
    private _categories  = new Map<string, CategoryDto>();
    private _budgets     = new Map<string, BudgetDto>();
    private _deletedIds  = new Set<string>();

    setBaseline(data: DemoBootstrapDto): void {
        this._baseline = data;
        this._baselineSubject.next(data);
    }

    // ── Merged read helpers ────────────────────────────────────────────────

    getMergedAccounts(): AccountDto[] {
        const base = this._baseline?.accounts ?? [];
        const merged = base
            .filter(a => !this._deletedIds.has(a.id!))
            .map(a => this._accounts.get(a.id!) ?? a);
        // Append session-created (new IDs not in baseline)
        this._accounts.forEach((a, id) => {
            if (!base.find(b => b.id === id)) merged.push(a);
        });
        return merged;
    }

    getMergedTransactions(): TransactionDto[] {
        const base = this._baseline?.recentTransactions ?? [];
        const merged = base
            .filter(t => !this._deletedIds.has(t.id!))
            .map(t => this._transactions.get(t.id!) ?? t);
        this._transactions.forEach((t, id) => {
            if (!base.find(b => b.id === id)) merged.push(t);
        });
        return merged.sort((a, b) =>
            new Date(b.txnTime!).getTime() - new Date(a.txnTime!).getTime());
    }

    getMergedCategories(): CategoryDto[] {
        const base = this._baseline?.categories ?? [];
        const merged = base
            .filter(c => !this._deletedIds.has(c.id!))
            .map(c => this._categories.get(c.id!) ?? c);
        this._categories.forEach((c, id) => {
            if (!base.find(b => b.id === id)) merged.push(c);
        });
        return merged;
    }

    getMergedBudgets(): BudgetDto[] {
        const base = this._baseline?.budgets ?? [];
        const merged = base
            .filter(b => !this._deletedIds.has(b.id!))
            .map(b => this._budgets.get(b.id!) ?? b);
        this._budgets.forEach((b, id) => {
            if (!base.find(bs => bs.id === id)) merged.push(b);
        });
        return merged;
    }

    getPositions(): PositionDto[] {
        return this._baseline?.positions ?? [];
    }

    getInstruments(): InstrumentDto[] {
        return this._baseline?.instruments ?? [];
    }

    // ── Write overlay handlers ─────────────────────────────────────────────

    handleFakeWrite(url: string, method: string, body: unknown): void {
        const b = body as Record<string, unknown> | null;
        if (!b) { return; }

        const id = (b['id'] as string) ?? '';
        method = method.toUpperCase();

        if (url.includes('/accounts')) {
            if (method === 'DELETE') { this._deletedIds.add(id); this._accounts.delete(id); }
            else this._accounts.set(id, b as unknown as AccountDto);
        } else if (url.includes('/transactions')) {
            if (method === 'DELETE') { this._deletedIds.add(id); this._transactions.delete(id); }
            else this._transactions.set(id, b as unknown as TransactionDto);
        } else if (url.includes('/categories')) {
            if (method === 'DELETE') { this._deletedIds.add(id); this._categories.delete(id); }
            else this._categories.set(id, b as unknown as CategoryDto);
        } else if (url.includes('/budgets')) {
            if (method === 'DELETE') { this._deletedIds.add(id); this._budgets.delete(id); }
            else this._budgets.set(id, b as unknown as BudgetDto);
        }
    }

    // ── Reset ─────────────────────────────────────────────────────────────

    reset(): Observable<DemoBootstrapDto | null> {
        this._accounts.clear();
        this._transactions.clear();
        this._categories.clear();
        this._budgets.clear();
        this._deletedIds.clear();

        return this.http
            .get<DemoBootstrapDto>(`${environment.apiUrl}/demo/bootstrap`)
            .pipe(
                tap(data => this.setBaseline(data)),
                catchError(() => of(null))
            );
    }
}
