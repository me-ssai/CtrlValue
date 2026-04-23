import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export interface AdminUser {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    role: string;
    tenantId: string;
    entities: EntityMembership[];
}

export interface EntityMembership {
    entityId: string;
    entityName: string;
    role: string;
}

export interface TenantRecord {
    id: string;
    name: string;
    contactEmail: string;
    isActive: boolean;
    createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
    private http = inject(HttpClient);

    private readonly base = `${environment.apiUrl}`;

    // ── Super Admin ────────────────────────────────────────────────────────────

    getAllUsers(): Observable<AdminUser[]> {
        return this.http.get<AdminUser[]>(`${this.base}/super-admin/users`);
    }

    updateUserRole(userId: string, role: string): Observable<any> {
        return this.http.put(`${this.base}/super-admin/users/${userId}/role`, { role });
    }

    impersonateUser(userId: string): Observable<{ message: string }> {
        return this.http.post<{ message: string }>(`${this.base}/super-admin/users/${userId}/impersonate`, {});
    }

    resetPasswordForUser(userId: string, isSiteAdmin = false): Observable<any> {
        const prefix = isSiteAdmin ? 'site-admin' : 'super-admin';
        return this.http.post(`${this.base}/${prefix}/users/${userId}/reset-password`, {});
    }

    deleteUser(userId: string): Observable<any> {
        return this.http.delete(`${this.base}/super-admin/users/${userId}`);
    }

    getAllTenants(): Observable<TenantRecord[]> {
        return this.http.get<TenantRecord[]>(`${this.base}/super-admin/tenants`);
    }

    createTenant(name: string, contactEmail: string): Observable<TenantRecord> {
        return this.http.post<TenantRecord>(`${this.base}/super-admin/tenants`, { name, contactEmail });
    }

    setTenantActive(tenantId: string, isActive: boolean): Observable<any> {
        return this.http.patch(`${this.base}/super-admin/tenants/${tenantId}/active?isActive=${isActive}`, {});
    }

    deleteTenant(tenantId: string): Observable<any> {
        return this.http.delete(`${this.base}/super-admin/tenants/${tenantId}`);
    }

    // ── Site Admin ─────────────────────────────────────────────────────────────

    getTenantUsers(): Observable<AdminUser[]> {
        return this.http.get<AdminUser[]>(`${this.base}/site-admin/users`);
    }

    inviteUser(entityId: string, email: string): Observable<any> {
        return this.http.post(`${this.base}/site-admin/entities/${entityId}/invite`, { email });
    }

    removeUserFromEntity(entityId: string, userId: string): Observable<any> {
        return this.http.delete(`${this.base}/site-admin/entities/${entityId}/users/${userId}`);
    }
}
