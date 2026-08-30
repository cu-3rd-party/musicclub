<script lang="ts">
    import "../app.css";
    import {browser} from "$app/environment";
    import {goto} from "$app/navigation";
    import {resolve} from "$app/paths";
    import {page} from "$app/state";
    import { retrieveRawInitData } from '@tma.js/sdk';

    import {ensureAuthenticated, telegramLogin} from "$lib/auth/store";

    let {children} = $props();

    let checkingAuth = $state(false);
    let guardRun = 0;
    let hasBeenAuthenticated = $state(false);
    let telegramLoginAttempted = false;

    function isTelegram(): boolean {
        return !!window["Telegram"]?.WebApp; // чтоб тайпскрипт не ругался попусту
    }

    function isAuthRoute(pathname: string): boolean {
        return pathname.startsWith("/auth");
    }

    async function guardRoute(pathname: string): Promise<void> {
        if (!browser) {
            return;
        }

        const currentRun = ++guardRun;

        // Only block rendering on the initial auth check.
        // Once authenticated, verify in the background without unmounting children.
        if (!hasBeenAuthenticated) {
            checkingAuth = true;
        }

        // 1. If there is already a session, just go to "/".
        const isAuthenticated = await ensureAuthenticated();
        if (currentRun !== guardRun) {
            return;
        }

        if (isAuthenticated) {
            hasBeenAuthenticated = true;
            checkingAuth = false;
            if (isAuthRoute(pathname)) {
                await goto(resolve("/"));
            }
            return;
        }

        // 2. No session but opened through Telegram → request telegramAuth and go to "/"
        //    once the user profile is acquired.
        console.log(`isTelegram: ${isTelegram()}`);
        if (!telegramLoginAttempted && isTelegram()) {
            const initData = retrieveRawInitData();
            console.log(initData);
            const user = await telegramLogin(initData);
            telegramLoginAttempted = true;
            if (currentRun !== guardRun) {
                return;
            }

            if (user) {
                hasBeenAuthenticated = true;
                checkingAuth = false;
                if (pathname !== "/") {
                    await goto(resolve("/"));
                }
                return;
            }
        }

        // 3. Otherwise go to the register page. Auth pages render as-is so
        //    login/register stay reachable for manual navigation.
        checkingAuth = false;
        if (!isAuthRoute(pathname)) {
            await goto(resolve("/auth"));
        }
    }

    $effect(() => {
        void guardRoute(page.url.pathname);
    });
</script>

{#if checkingAuth}
    <div class="bg-background min-h-screen"></div>
{:else}
    {@render children?.()}
{/if}
