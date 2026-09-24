<script lang="ts">
    import BottomNav from "$lib/components/bottom-nav.svelte";
    import MusicIcon from "@lucide/svelte/icons/music";
    import UserIcon from "@lucide/svelte/icons/user";
    import CalendarIcon from "@lucide/svelte/icons/calendar";
    import YandexLoginModal from "$lib/components/YandexLoginModal.svelte";
    import {getYandexLogin, type YandexLoginDto} from "$lib/api/yandex-login";
    import {onMount} from "svelte";

    let {children} = $props();

    const navItems = [
        {label: "Песни", href: "/app/songs", icon: MusicIcon},
        {label: "Календарь", href: "/app/calendar", icon: CalendarIcon},
        {label: "Профиль", href: "/app/profile", icon: UserIcon},
    ];

    let showModal = $state(false);
    let yandexLogin = $state<YandexLoginDto | null>(null);

    onMount(async () => {
        try {
            yandexLogin = await getYandexLogin();
            // Показываем модалку если YandexLogin не установлен
            if (!yandexLogin?.hasYandexLogin) {
                showModal = true;
            }
        } catch (err) {
            console.error("Failed to check YandexLogin:", err);
        }
    });

    function handleSaved(result: YandexLoginDto) {
        yandexLogin = result;
        showModal = false;
    }
</script>

<div class="flex h-screen flex-col">
    <main class="min-h-0 flex-1 overflow-y-auto" id="app-container">
        {@render children?.()}
    </main>

    <BottomNav
        items={navItems}
        class="shrink-0"
    />

    <YandexLoginModal
        bind:open={showModal}
        {yandexLogin}
        on:saved={(e) => handleSaved(e.detail)}
        on:close={() => (showModal = false)}
    />
</div>
