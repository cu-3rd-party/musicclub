import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (authService.isAuthenticated()) {
        return true;
    }

    // Сохраняем URL для возврата после входа
    router.navigate(['/auth/login'], {
        queryParams: { returnUrl: state.url }
    });
    return false;
};

/**
 * Guard для публичных страниц (login, register)
 * Перенаправляет на главную если пользователь уже авторизован
 */
export const publicGuard: CanActivateFn = (route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (authService.isAuthenticated()) {
        const returnUrl = route.queryParams['returnUrl'] || '/app/songs';
        router.navigate([returnUrl]);
        return false;
    }

    return true;
};
