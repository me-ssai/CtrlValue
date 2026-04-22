import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of, tap } from 'rxjs';
import { environment } from '@env/environment';

// ═══════════════════════════════════════════════════════════════════════════
// Entity Models
// ═══════════════════════════════════════════════════════════════════════════

export interface Entity {
    id: string;
    name: string;
    baseCurrency: string;
    country: string;
    users?: EntityUser[];
    createdAt: string;
}

export interface EntityUser {
    userId: string;
    userName: string;
    userEmail: string;
    role: 'OWNER' | 'VIEWER' | 'EDITOR';
    joinedAt: string;
}

export interface CreateEntityRequest {
    name: string;
    baseCurrency: string;
    country: string;
}

export interface UpdateEntityRequest {
    name: string;
    baseCurrency: string;
    country: string;
}

export interface AddEntityUserRequest {
    email: string;
    role: 'OWNER' | 'VIEWER' | 'EDITOR';
}

export interface UpdateEntityUserRequest {
    role: 'OWNER' | 'VIEWER' | 'EDITOR';
}

@Injectable({ providedIn: 'root' })
export class EntityService {
    private readonly ENTITY_KEY = 'ctrlvalue_current_entity';

    private currentEntitySubject = new BehaviorSubject<Entity | null>(this.getStoredEntity());
    currentEntity$ = this.currentEntitySubject.asObservable();

    constructor(private http: HttpClient) { }

    get currentEntity(): Entity | null {
        return this.currentEntitySubject.value;
    }

    get currentEntityId(): string | null {
        return this.currentEntity?.id || null;
    }

    // Get all entities for current user
    getEntities(): Observable<Entity[]> {
        return this.http.get<Entity[]>(`${environment.apiUrl}/entities`);
    }

    // Get or create default entity (called after login)
    getOrCreateDefaultEntity(): Observable<Entity> {
        // In demo mode, entity comes from the bootstrap payload — no API call needed
        if (environment.demo) {
            // DemoStateService is not injected here to avoid circular deps;
            // the APP_INITIALIZER already sets the demo entity before this is called.
            // Return the cached entity if already set, otherwise a minimal placeholder.
            const cached = this.currentEntitySubject.value;
            if (cached) return of(cached);
            return of({
                id: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
                name: 'Demo Workspace',
                baseCurrency: 'AUD',
                country: 'AU',
                createdAt: new Date().toISOString(),
            });
        }
        return this.http.get<Entity>(`${environment.apiUrl}/entities/default`).pipe(
            tap(entity => this.setCurrentEntity(entity))
        );
    }

    // Get entity by ID
    getEntityById(id: string): Observable<Entity> {
        return this.http.get<Entity>(`${environment.apiUrl}/entities/${id}`);
    }

    // Create entity
    createEntity(request: CreateEntityRequest): Observable<Entity> {
        return this.http.post<Entity>(`${environment.apiUrl}/entities`, request);
    }

    // Update entity
    updateEntity(id: string, request: UpdateEntityRequest): Observable<Entity> {
        return this.http.put<Entity>(`${environment.apiUrl}/entities/${id}`, request);
    }

    // Delete entity
    deleteEntity(id: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/entities/${id}`);
    }

    // Get entity users
    getEntityUsers(entityId: string): Observable<EntityUser[]> {
        return this.http.get<EntityUser[]>(`${environment.apiUrl}/entities/${entityId}/users`);
    }

    // Add user to entity
    addUserToEntity(entityId: string, request: AddEntityUserRequest): Observable<EntityUser> {
        return this.http.post<EntityUser>(`${environment.apiUrl}/entities/${entityId}/users`, request);
    }

    // Update user role
    updateUserRole(entityId: string, userId: string, request: UpdateEntityUserRequest): Observable<EntityUser> {
        return this.http.put<EntityUser>(`${environment.apiUrl}/entities/${entityId}/users/${userId}`, request);
    }

    // Remove user from entity
    removeUserFromEntity(entityId: string, userId: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/entities/${entityId}/users/${userId}`);
    }

    // Set current entity
    setCurrentEntity(entity: Entity): void {
        localStorage.setItem(this.ENTITY_KEY, JSON.stringify(entity));
        this.currentEntitySubject.next(entity);
    }

    // Clear current entity
    clearCurrentEntity(): void {
        localStorage.removeItem(this.ENTITY_KEY);
        this.currentEntitySubject.next(null);
    }

    // Get stored entity
    private getStoredEntity(): Entity | null {
        const entityJson = localStorage.getItem(this.ENTITY_KEY);
        return entityJson ? JSON.parse(entityJson) : null;
    }
}
