import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

// ═══════════════════════════════════════════════════════════════════════════
// Valuation Models
// ═══════════════════════════════════════════════════════════════════════════

export interface Valuation {
    id: string;
    accountId: string;
    accountName: string;
    asOfDate: string;
    value: number;
    currency: string;
    source?: string;
    notes?: string;
    createdAt: string;
}

export interface CreateValuationRequest {
    accountId: string;
    asOfDate: string;
    value: number;
    currency: string;
    source?: string;
    notes?: string;
}

export interface UpdateValuationRequest {
    value: number;
    asOfDate?: string;
    notes?: string;
    source?: string;
}

// ═══════════════════════════════════════════════════════════════════════════
// Depreciation Schedule Models
// ═══════════════════════════════════════════════════════════════════════════

export interface DepreciationSchedule {
    id: string;
    accountId: string;
    accountName: string;
    method: string;
    purchasePrice: number;
    purchaseDate: string;
    usefulLifeYears?: number;
    salvageValue?: number;
    annualDepreciationRate?: number;
    currentValue?: number;
    accumulatedDepreciation?: number;
    createdAt: string;
}

export interface CreateDepreciationScheduleRequest {
    accountId: string;
    method: 'STRAIGHT_LINE' | 'DECLINING_BALANCE' | 'REDBOOK';
    purchasePrice: number;
    purchaseDate: string;
    usefulLifeYears?: number;
    salvageValue?: number;
    annualDepreciationRate?: number;
}

export interface UpdateDepreciationScheduleRequest {
    usefulLifeYears?: number;
    salvageValue?: number;
    annualDepreciationRate?: number;
}

@Injectable({ providedIn: 'root' })
export class ValuationService {
    constructor(private http: HttpClient) { }

    getValuations(accountId?: string, startDate?: string, endDate?: string): Observable<Valuation[]> {
        const params: Record<string, string> = {};
        if (accountId) params['accountId'] = accountId;
        if (startDate) params['startDate'] = startDate;
        if (endDate) params['endDate'] = endDate;
        return this.http.get<Valuation[]>(`${environment.apiUrl}/valuations`, { params });
    }

    getLatestValuation(accountId: string): Observable<Valuation> {
        return this.http.get<Valuation>(`${environment.apiUrl}/valuations/account/${accountId}/latest`);
    }

    createValuation(request: CreateValuationRequest): Observable<Valuation> {
        return this.http.post<Valuation>(`${environment.apiUrl}/valuations`, request);
    }

    updateValuation(id: string, request: UpdateValuationRequest): Observable<Valuation> {
        return this.http.put<Valuation>(`${environment.apiUrl}/valuations/${id}`, request);
    }

    deleteValuation(id: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/valuations/${id}`);
    }
}

@Injectable({ providedIn: 'root' })
export class DepreciationScheduleService {
    constructor(private http: HttpClient) { }

    getDepreciationSchedules(): Observable<DepreciationSchedule[]> {
        return this.http.get<DepreciationSchedule[]>(`${environment.apiUrl}/depreciationschedules`);
    }

    getDepreciationScheduleById(id: string): Observable<DepreciationSchedule> {
        return this.http.get<DepreciationSchedule>(`${environment.apiUrl}/depreciationschedules/${id}`);
    }

    getDepreciationScheduleByAccount(accountId: string): Observable<DepreciationSchedule> {
        return this.http.get<DepreciationSchedule>(`${environment.apiUrl}/depreciationschedules/account/${accountId}`);
    }

    getCurrentValue(id: string, asOfDate?: string): Observable<{ currentValue: number; asOfDate: string }> {
        const params: Record<string, string> = {};
        if (asOfDate) params['asOfDate'] = asOfDate;
        return this.http.get<{ currentValue: number; asOfDate: string }>(`${environment.apiUrl}/depreciationschedules/${id}/current-value`, { params });
    }

    createDepreciationSchedule(request: CreateDepreciationScheduleRequest): Observable<DepreciationSchedule> {
        return this.http.post<DepreciationSchedule>(`${environment.apiUrl}/depreciationschedules`, request);
    }

    updateDepreciationSchedule(id: string, request: UpdateDepreciationScheduleRequest): Observable<DepreciationSchedule> {
        return this.http.put<DepreciationSchedule>(`${environment.apiUrl}/depreciationschedules/${id}`, request);
    }

    deleteDepreciationSchedule(id: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/depreciationschedules/${id}`);
    }
}
