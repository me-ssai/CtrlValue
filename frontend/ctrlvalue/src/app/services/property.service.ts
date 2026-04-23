import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export interface Property {
    id: string;
    accountId: string;
    accountName: string;
    address: string;
    suburb?: string;
    state?: string;
    postCode?: string;
    country: string;
    propertyType: string;
    bedrooms?: number;
    bathrooms?: number;
    carSpaces?: number;
    landSizeSqm?: number;
    floorSizeSqm?: number;
    yearBuilt?: number;
    purchasePrice: number;
    purchaseDate: string;
    isRental: boolean;
    weeklyRentTarget?: number;
    currentValue: number;
    latestValuationValue?: number;
    latestValuationAsOfDate?: string;
    createdAt: string;
}

export interface CreatePropertyRequest {
    address: string;
    suburb?: string;
    state?: string;
    postCode?: string;
    country: string;
    propertyType: string;
    bedrooms?: number;
    bathrooms?: number;
    carSpaces?: number;
    landSizeSqm?: number;
    floorSizeSqm?: number;
    yearBuilt?: number;
    purchasePrice: number;
    purchaseDate: string;
    isRental: boolean;
    weeklyRentTarget?: number;
    entityId: string;
    currency: string;
}

export interface UpdatePropertyRequest {
    address: string;
    suburb?: string;
    state?: string;
    postCode?: string;
    country: string;
    propertyType: string;
    bedrooms?: number;
    bathrooms?: number;
    carSpaces?: number;
    landSizeSqm?: number;
    floorSizeSqm?: number;
    yearBuilt?: number;
    isRental: boolean;
    weeklyRentTarget?: number;
}

@Injectable({ providedIn: 'root' })
export class PropertyService {
    private http = inject(HttpClient);


    getProperties(): Observable<Property[]> {
        return this.http.get<Property[]>(`${environment.apiUrl}/property`);
    }

    getPropertyById(id: string): Observable<Property> {
        return this.http.get<Property>(`${environment.apiUrl}/property/${id}`);
    }

    getPropertyByAccountId(accountId: string): Observable<Property> {
        return this.http.get<Property>(`${environment.apiUrl}/property/account/${accountId}`);
    }

    createProperty(request: CreatePropertyRequest): Observable<Property> {
        return this.http.post<Property>(`${environment.apiUrl}/property`, request);
    }

    updateProperty(id: string, request: UpdatePropertyRequest): Observable<Property> {
        return this.http.put<Property>(`${environment.apiUrl}/property/${id}`, request);
    }

    deleteProperty(id: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/property/${id}`);
    }
}
