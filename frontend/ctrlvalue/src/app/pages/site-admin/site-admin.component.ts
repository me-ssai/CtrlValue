import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService, AdminUser } from '../../services/admin.service';
import { EntityService, Entity } from '../../services/entity.service';

@Component({
    selector: 'app-site-admin',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './site-admin.component.html',
    styleUrls: ['./site-admin.component.css']
})
export class SiteAdminComponent implements OnInit {
    activeTab: 'clients' | 'users' = 'clients';
    entities: Entity[] = [];
    tenantUsers: AdminUser[] = [];
    inviteEntityId = '';
    inviteEmail = '';
    statusMessage = '';
    isError = false;

    constructor(private adminService: AdminService, private entityService: EntityService) { }

    ngOnInit() {
        this.entityService.getEntities().subscribe({ next: e => this.entities = e });
        this.adminService.getTenantUsers().subscribe({ next: u => this.tenantUsers = u });
    }

    getUsersForEntity(entityId: string): AdminUser[] {
        return this.tenantUsers.filter(u => u.entities.some(e => e.entityId === entityId));
    }

    getRoleForEntity(user: AdminUser, entityId: string): string {
        return user.entities.find(e => e.entityId === entityId)?.role ?? '';
    }

    openInvite(entityId: string) {
        this.inviteEntityId = this.inviteEntityId === entityId ? '' : entityId;
        this.inviteEmail = '';
    }

    sendInvite(entityId: string) {
        if (!this.inviteEmail) return;
        this.adminService.inviteUser(entityId, this.inviteEmail).subscribe({
            next: () => {
                this.showStatus(`Invite sent to ${this.inviteEmail}`);
                this.inviteEntityId = '';
                this.inviteEmail = '';
                this.adminService.getTenantUsers().subscribe({ next: u => this.tenantUsers = u });
            },
            error: (e) => this.showStatus(e?.error?.error || 'Failed to send invite', true)
        });
    }

    removeUser(entityId: string, userId: string) {
        if (!confirm('Remove this user from the entity?')) return;
        this.adminService.removeUserFromEntity(entityId, userId).subscribe({
            next: () => {
                this.showStatus('User removed from entity');
                this.tenantUsers = this.tenantUsers.map(u => ({
                    ...u,
                    entities: u.entities.filter(e => !(e.entityId === entityId && u.id === userId))
                }));
            },
            error: () => this.showStatus('Failed to remove user', true)
        });
    }

    deleteEntity(entity: Entity) {
        const msg = `Delete "${entity.name}" and ALL its data?\n\nThis will permanently remove all accounts, transactions, categories, budgets, positions, valuations, and import history. This cannot be undone.`;
        if (!confirm(msg)) return;

        this.entityService.deleteEntity(entity.id).subscribe({
            next: () => {
                this.entities = this.entities.filter(e => e.id !== entity.id);
                // Clear invite state if open for this entity
                if (this.inviteEntityId === entity.id) {
                    this.inviteEntityId = '';
                    this.inviteEmail = '';
                }
                this.tenantUsers = this.tenantUsers.map(u => ({
                    ...u,
                    entities: u.entities.filter(e => e.entityId !== entity.id)
                }));
                this.showStatus(`Entity "${entity.name}" and all its data have been deleted.`);
            },
            error: () => this.showStatus(`Failed to delete entity "${entity.name}"`, true)
        });
    }

    resetPassword(user: AdminUser) {
        this.adminService.resetPasswordForUser(user.id, true).subscribe({
            next: () => this.showStatus(`Password reset email sent to ${user.email}`),
            error: () => this.showStatus('Failed to send reset email', true)
        });
    }

    private showStatus(msg: string, error = false) {
        this.statusMessage = msg;
        this.isError = error;
        setTimeout(() => this.statusMessage = '', 3500);
    }
}
