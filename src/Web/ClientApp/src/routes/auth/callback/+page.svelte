<script lang="ts">
    import {onMount} from "svelte";
    import {goto} from "$app/navigation";
    import {page} from "$app/state";
    import {completeExternalLogin} from "$lib/auth/store";
    import {DEFAULT_APP_PAGE} from "$lib/config";

    // Сюда бэкенд возвращает после входа у провайдера: ?code=<одноразовый код>&returnUrl=<локальный путь>.
    function safeReturnUrl(value: string | null): string {
        // Только локальный путь — защита от open redirect.
        if (!value || !value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\")) {
            return DEFAULT_APP_PAGE;
        }
        return value;
    }

    function backToLogin(): Promise<void> {
        // eslint-disable-next-line svelte/no-navigation-without-resolve -- абсолютный путь с query
        return goto("/auth?error=external", {replaceState: true});
    }

    onMount(async () => {
        const code = page.url.searchParams.get("code");
        const returnUrl = safeReturnUrl(page.url.searchParams.get("returnUrl"));
        if (!code) {
            await backToLogin();
            return;
        }

        try {
            await completeExternalLogin(code);
            // eslint-disable-next-line svelte/no-navigation-without-resolve -- returnUrl уже локальный путь
            await goto(returnUrl, {replaceState: true});
        } catch {
            await backToLogin();
        }
    });
</script>

<p class="text-center text-sm text-muted-foreground">Входим…</p>
