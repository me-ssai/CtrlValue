import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

// ═══════════════════════════════════════════════════════════════════════════
// Instrument Models
// ═══════════════════════════════════════════════════════════════════════════

export interface Instrument {
    id: string;
    symbol: string;
    name: string;
    instrumentType: string;
    currency: string;
    exchange?: string;
    externalSymbol?: string;
    priceProvider?: string;
    priceUnit: string;
    latestPrice?: number;
    latestPriceDate?: string;
    createdAt: string;
    // Bond fields
    issuer?: string;
    faceValue?: number;
    couponRate?: number;
    couponFrequency?: string;
    maturityDate?: string;
    issueDate?: string;
    creditRating?: string;
    // ETF / Fund fields
    expenseRatio?: number;
    distributionYield?: number;
    distributionFrequency?: string;
    underlyingIndex?: string;
}

export interface CreateInstrumentRequest {
    symbol: string;
    name: string;
    instrumentType: 'STOCK' | 'BOND' | 'ETF' | 'METAL' | 'CRYPTO' | 'FUND' | 'OTHER';
    currency: string;
    exchange?: string;
    externalSymbol?: string;
    priceProvider?: string;
    priceUnit?: string;
    // Bond fields
    issuer?: string;
    faceValue?: number;
    couponRate?: number;
    couponFrequency?: string;
    maturityDate?: string;
    issueDate?: string;
    creditRating?: string;
    // ETF / Fund fields
    expenseRatio?: number;
    distributionYield?: number;
    distributionFrequency?: string;
    underlyingIndex?: string;
}

export interface UpdateInstrumentRequest {
    name: string;
    currency: string;
    exchange?: string;
    externalSymbol?: string;
    priceProvider?: string;
    priceUnit?: string;
    // Bond fields
    issuer?: string;
    faceValue?: number;
    couponRate?: number;
    couponFrequency?: string;
    maturityDate?: string;
    issueDate?: string;
    creditRating?: string;
    // ETF / Fund fields
    expenseRatio?: number;
    distributionYield?: number;
    distributionFrequency?: string;
    underlyingIndex?: string;
}

// ═══════════════════════════════════════════════════════════════════════════
// Position Models
// ═══════════════════════════════════════════════════════════════════════════

export interface Position {
    id: string;
    accountId: string;
    accountName: string;
    instrumentId?: string;
    instrumentSymbol?: string;
    instrumentName?: string;
    instrumentType?: string;
    quantity: number;
    unit: string;
    costBasisTotal?: number;
    costBasisPerUnit?: number;
    currentPrice?: number;
    currentValue?: number;
    unrealizedGainLoss?: number;
    unrealizedGainLossPercent?: number;
    currency?: string;
    openedAt: string;
    createdAt: string;
}

export interface CreatePositionRequest {
    accountId: string;
    instrumentId?: string;
    quantity: number;
    unit: string;
    costBasisTotal?: number;
    openedAt?: string;
}

export interface UpdatePositionRequest {
    quantity: number;
    costBasisTotal?: number;
}

export interface PositionPerformance {
    currentValue?: number;
    costBasis?: number;
    unrealizedGainLoss?: number;
    unrealizedGainLossPercent?: number;
    currentPrice?: number;
    averageCostPerUnit?: number;
}

// ═══════════════════════════════════════════════════════════════════════════
// Instrument Search
// ═══════════════════════════════════════════════════════════════════════════

export interface InstrumentSearchResult {
    symbol: string;
    name: string;
    type: string;
    exchange?: string;
    currency: string;
    isAlreadyTracked: boolean;
}

@Injectable({ providedIn: 'root' })
export class InstrumentService {
    constructor(private http: HttpClient) { }

    getInstruments(type?: string): Observable<Instrument[]> {
        const params: Record<string, string> = {};
        if (type) params['type'] = type;
        return this.http.get<Instrument[]>(`${environment.apiUrl}/instruments`, { params });
    }

    getInstrumentById(id: string): Observable<Instrument> {
        return this.http.get<Instrument>(`${environment.apiUrl}/instruments/${id}`);
    }

    getInstrumentBySymbol(symbol: string): Observable<Instrument> {
        return this.http.get<Instrument>(`${environment.apiUrl}/instruments/symbol/${symbol}`);
    }

    createInstrument(request: CreateInstrumentRequest): Observable<Instrument> {
        return this.http.post<Instrument>(`${environment.apiUrl}/instruments`, request);
    }

    updateInstrument(id: string, request: UpdateInstrumentRequest): Observable<Instrument> {
        return this.http.put<Instrument>(`${environment.apiUrl}/instruments/${id}`, request);
    }

    deleteInstrument(id: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/instruments/${id}`);
    }

    getDefaultInstruments(): Observable<Instrument[]> {
        return this.http.get<Instrument[]>(`${environment.apiUrl}/instruments/defaults`);
    }

    searchInstruments(query: string, type?: string, exchange?: string): Observable<InstrumentSearchResult[]> {
        const params: Record<string, string> = { query };
        if (type) params['type'] = type;
        if (exchange) params['exchange'] = exchange;
        return this.http.get<InstrumentSearchResult[]>(`${environment.apiUrl}/instruments/search`, { params });
    }
}

@Injectable({ providedIn: 'root' })
export class PositionService {
    constructor(private http: HttpClient) { }

    getPositions(accountId?: string): Observable<Position[]> {
        const params: Record<string, string> = {};
        if (accountId) params['accountId'] = accountId;
        return this.http.get<Position[]>(`${environment.apiUrl}/positions`, { params });
    }

    getPositionById(id: string): Observable<Position> {
        return this.http.get<Position>(`${environment.apiUrl}/positions/${id}`);
    }

    getPositionPerformance(id: string): Observable<PositionPerformance> {
        return this.http.get<PositionPerformance>(`${environment.apiUrl}/positions/${id}/performance`);
    }

    getPositionValue(id: string): Observable<{ value: number }> {
        return this.http.get<{ value: number }>(`${environment.apiUrl}/positions/${id}/value`);
    }

    createPosition(request: CreatePositionRequest): Observable<Position> {
        return this.http.post<Position>(`${environment.apiUrl}/positions`, request);
    }

    updatePosition(id: string, request: UpdatePositionRequest): Observable<Position> {
        return this.http.put<Position>(`${environment.apiUrl}/positions/${id}`, request);
    }

    deletePosition(id: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/positions/${id}`);
    }
}
