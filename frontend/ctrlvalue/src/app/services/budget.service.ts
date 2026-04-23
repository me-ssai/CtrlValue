import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

// ═══════════════════════════════════════════════════════════════════════════
// Budget Models
// ═══════════════════════════════════════════════════════════════════════════

export interface Budget {
    id: string;
    entityId: string;
    categoryId: string;
    categoryName: string;
    periodType: string;
    periodStart: string;
    periodEnd: string;
    amount: number;
    currency: string;
    actualSpent?: number;
    remaining?: number;
    percentUsed?: number;
    createdAt: string;
}

export interface CreateBudgetRequest {
    categoryId: string;
    periodType: 'MONTHLY' | 'QUARTERLY' | 'ANNUAL';
    periodStart: string;
    periodEnd: string;
    amount: number;
    currency: string;
}

export interface UpdateBudgetRequest {
    amount: number;
    periodEnd?: string;
}

@Injectable({ providedIn: 'root' })
export class BudgetService {
    private http = inject(HttpClient);


    getBudgets(categoryId?: string): Observable<Budget[]> {
        const params: Record<string, string> = {};
        if (categoryId) params['categoryId'] = categoryId;
        return this.http.get<Budget[]>(`${environment.apiUrl}/budgets`, { params });
    }

    getActiveBudgets(asOfDate?: string): Observable<Budget[]> {
        const params: Record<string, string> = {};
        if (asOfDate) params['asOfDate'] = asOfDate;
        return this.http.get<Budget[]>(`${environment.apiUrl}/budgets/active`, { params });
    }

    getBudgetById(id: string): Observable<Budget> {
        return this.http.get<Budget>(`${environment.apiUrl}/budgets/${id}`);
    }

    createBudget(request: CreateBudgetRequest): Observable<Budget> {
        return this.http.post<Budget>(`${environment.apiUrl}/budgets`, request);
    }

    updateBudget(id: string, request: UpdateBudgetRequest): Observable<Budget> {
        return this.http.put<Budget>(`${environment.apiUrl}/budgets/${id}`, request);
    }

    deleteBudget(id: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/budgets/${id}`);
    }
}
