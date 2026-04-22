import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class TokenStoreService {
    // Tokens are stored as httpOnly cookies — not readable by JavaScript.
    // This service is kept for compatibility but no longer reads/writes tokens.
    getToken(): string | null { return null; }
    setToken(_token: string | null): void { }
}
